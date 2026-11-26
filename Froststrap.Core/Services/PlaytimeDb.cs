// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Data.Sqlite;
using System.Buffers;
using System.Diagnostics;

namespace Froststrap.Services;

internal sealed class PlaytimeDb : IDisposable
{
    private const int CurrentSchemaVersion = 1;
    private const string DbFileName = "playtime.db";

    private readonly string _dbPath;
    private readonly SemaphoreSlim _writerLock = new(1, 1);
    private SqliteConnection? _connection;
    private bool _disposed;

    public PlaytimeDb(string? customDataRoot = null)
    {
        var dataRoot = customDataRoot ?? Paths.DataRoot;
        _dbPath = Path.Combine(dataRoot, DbFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
    }

    private async Task<SqliteConnection> GetConnectionAsync()
    {
        if (_connection is { State: System.Data.ConnectionState.Open })
            return _connection;

        _connection = new SqliteConnection($"Data Source={_dbPath};Pooling=False;");
        await _connection.OpenAsync();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
        await pragma.ExecuteNonQueryAsync();

        await MigrateAsync(_connection);
        return _connection;
    }

    private async Task MigrateAsync(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var currentVersion = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        if (currentVersion >= CurrentSchemaVersion)
            return;

        if (currentVersion == 0)
        {
            await CreateSchemaV1Async(connection);
        }

        using var versionCmd = connection.CreateCommand();
        versionCmd.CommandText = $"PRAGMA user_version = {CurrentSchemaVersion};";
        await versionCmd.ExecuteNonQueryAsync();
    }

    private static async Task CreateSchemaV1Async(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;

            cmd.CommandText = """
                CREATE TABLE sessions (
                    id TEXT PRIMARY KEY,
                    user_id INTEGER NOT NULL,
                    universe_id INTEGER NOT NULL,
                    place_id INTEGER NOT NULL,
                    job_id TEXT,
                    started_at INTEGER NOT NULL,
                    ended_at INTEGER,
                    last_seen_at INTEGER NOT NULL,
                    updated_at INTEGER NOT NULL,
                    synced_at INTEGER,
                    deleted INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX idx_sessions_user_universe_started ON sessions(user_id, universe_id, started_at);
                CREATE TABLE games (
                    universe_id INTEGER PRIMARY KEY,
                    name TEXT,
                    icon_url TEXT,
                    fetched_at INTEGER
                );
                """;
            await cmd.ExecuteNonQueryAsync();
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public async Task<SessionRow> OpenSessionAsync(long userId, long universeId, long placeId, string? jobId = null, CancellationToken ct = default)
    {
        await _writerLock.WaitAsync();
        try
        {
            var id = GenerateUlid();
            var now = NowUnix();

            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO sessions (id, user_id, universe_id, place_id, job_id, started_at, ended_at, last_seen_at, updated_at, deleted)
                VALUES ($id, $userId, $universeId, $placeId, $jobId, $startedAt, NULL, $lastSeenAt, $updatedAt, 0);
                """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.Parameters.AddWithValue("$universeId", universeId);
            cmd.Parameters.AddWithValue("$placeId", placeId);
            cmd.Parameters.AddWithValue("$jobId", jobId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$startedAt", now);
            cmd.Parameters.AddWithValue("$lastSeenAt", now);
            cmd.Parameters.AddWithValue("$updatedAt", now);
            await cmd.ExecuteNonQueryAsync(ct);

            return new SessionRow(id, userId, universeId, placeId, jobId, now, null, now, now, 0);
        }
        finally
        {
            _writerLock.Release();
        }
    }

    public async Task<bool> TouchSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await _writerLock.WaitAsync();
        try
        {
            var now = NowUnix();

            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE sessions
                SET last_seen_at = $now, updated_at = $now
                WHERE id = $id AND deleted = 0 AND ended_at IS NULL;
                """;
            cmd.Parameters.AddWithValue("$id", sessionId);
            cmd.Parameters.AddWithValue("$now", now);
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows > 0;
        }
        finally
        {
            _writerLock.Release();
        }
    }

    public async Task<EndSessionResult> EndSessionAsync(string sessionId, long? endedAt = null, CancellationToken ct = default)
    {
        await _writerLock.WaitAsync();
        try
        {
            var now = NowUnix();
            var endTime = endedAt ?? now;

            var conn = await GetConnectionAsync();

            // First, get current session values for clamping
            using var selectCmd = conn.CreateCommand();
            selectCmd.CommandText = "SELECT started_at, last_seen_at FROM sessions WHERE id = $id AND deleted = 0 AND ended_at IS NULL;";
            selectCmd.Parameters.AddWithValue("$id", sessionId);

            long startedAt = 0;
            long lastSeenAt = 0;
            using (var reader = await selectCmd.ExecuteReaderAsync(ct))
            {
                if (await reader.ReadAsync(ct))
                {
                    startedAt = reader.GetInt64(0);
                    lastSeenAt = reader.GetInt64(1);
                }
                else
                {
                    return new EndSessionResult(false, 0, 0, 0);
                }
            }

            // Apply clamping in C#
            if (endTime < startedAt)
                endTime = startedAt;
            else if (endTime < lastSeenAt)
                endTime = lastSeenAt;

            var finalLastSeenAt = endTime > lastSeenAt ? endTime : lastSeenAt;

            using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = """
                UPDATE sessions
                SET ended_at = $endedAt,
                    last_seen_at = $lastSeenAt,
                    updated_at = $now
                WHERE id = $id AND deleted = 0 AND ended_at IS NULL;
                """;
            updateCmd.Parameters.AddWithValue("$id", sessionId);
            updateCmd.Parameters.AddWithValue("$endedAt", endTime);
            updateCmd.Parameters.AddWithValue("$lastSeenAt", finalLastSeenAt);
            updateCmd.Parameters.AddWithValue("$now", now);
            var rows = await updateCmd.ExecuteNonQueryAsync(ct);
            
            if (rows > 0)
            {
                return new EndSessionResult(true, endTime, finalLastSeenAt, now);
            }
            return new EndSessionResult(false, 0, 0, 0);
        }
        finally
        {
            _writerLock.Release();
        }
    }

    public sealed record EndSessionResult(bool Success, long EndedAt, long LastSeenAt, long UpdatedAt);

    public async Task<int> CloseOrphansAsync(CancellationToken ct = default)
    {
        await _writerLock.WaitAsync();
        try
        {
            var now = NowUnix();

            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE sessions
                SET ended_at = last_seen_at, updated_at = $now
                WHERE deleted = 0 AND ended_at IS NULL;
                """;
            cmd.Parameters.AddWithValue("$now", now);
            return await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _writerLock.Release();
        }
    }

    public async Task<List<UniverseTotal>> GetTotalsPerUniverseAsync(long userId, CancellationToken ct = default)
    {
        await _writerLock.WaitAsync();
        try
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT universe_id, SUM(COALESCE(ended_at, last_seen_at) - started_at) as total_seconds
                FROM sessions
                WHERE user_id = $userId AND deleted = 0
                GROUP BY universe_id
                ORDER BY total_seconds DESC;
                """;
            cmd.Parameters.AddWithValue("$userId", userId);

            var results = new List<UniverseTotal>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(new UniverseTotal(
                    reader.GetInt64(0),
                    Math.Max(0, reader.GetInt64(1))
                ));
            }
            return results;
        }
        finally
        {
            _writerLock.Release();
        }
    }

    public async Task<List<SessionRow>> GetSessionsAsync(long userId, long universeId, CancellationToken ct = default)
    {
        await _writerLock.WaitAsync();
        try
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, user_id, universe_id, place_id, job_id, started_at, ended_at, last_seen_at, updated_at, deleted
                FROM sessions
                WHERE user_id = $userId AND universe_id = $universeId AND deleted = 0
                ORDER BY started_at DESC;
                """;
            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.Parameters.AddWithValue("$universeId", universeId);

            var results = new List<SessionRow>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(new SessionRow(
                    reader.GetString(0),
                    reader.GetInt64(1),
                    reader.GetInt64(2),
                    reader.GetInt64(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetInt64(5),
                    reader.IsDBNull(6) ? null : reader.GetInt64(6),
                    reader.GetInt64(7),
                    reader.GetInt64(8),
                    reader.GetInt32(9)
                ));
            }
            return results;
        }
        finally
        {
            _writerLock.Release();
        }
    }

    public async Task<int> DeleteAllForUserAsync(long userId, CancellationToken ct = default)
    {
        await _writerLock.WaitAsync();
        try
        {
            var now = NowUnix();

            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE sessions
                SET deleted = 1, updated_at = $now
                WHERE user_id = $userId AND deleted = 0;
                """;
            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.Parameters.AddWithValue("$now", now);
            return await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _writerLock.Release();
        }
    }

    public async Task<bool> UpsertGameAsync(long universeId, string? name, string? iconUrl, CancellationToken ct = default)
    {
        await _writerLock.WaitAsync();
        try
        {
            var now = NowUnix();

            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO games (universe_id, name, icon_url, fetched_at)
                VALUES ($universeId, $name, $iconUrl, $fetchedAt)
                ON CONFLICT(universe_id) DO UPDATE SET
                    name = COALESCE($name, name),
                    icon_url = COALESCE($iconUrl, icon_url),
                    fetched_at = $fetchedAt;
                """;
            cmd.Parameters.AddWithValue("$universeId", universeId);
            cmd.Parameters.AddWithValue("$name", name ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$iconUrl", iconUrl ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$fetchedAt", now);
            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }
        finally
        {
            _writerLock.Release();
        }
    }

    public async Task<GameRow?> GetGameAsync(long universeId, CancellationToken ct = default)
    {
        await _writerLock.WaitAsync();
        try
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT universe_id, name, icon_url, fetched_at FROM games WHERE universe_id = $universeId;";
            cmd.Parameters.AddWithValue("$universeId", universeId);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new GameRow(
                    reader.GetInt64(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.GetInt64(3)
                );
            }
            return null;
        }
        finally
        {
            _writerLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _connection?.Dispose();
        }
        finally
        {
            _writerLock.Dispose();
        }
    }

    private static string GenerateUlid()
    {
        // Generate a ULID-compatible 26-char string (Crockford's Base32)
        // 48-bit timestamp + 80-bit randomness
        var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = new byte[10];
        Random.Shared.NextBytes(random);

        const string encoding = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        Span<char> result = stackalloc char[26];

        // Encode 48-bit timestamp (10 chars)
        for (int i = 9; i >= 0; i--)
        {
            result[i] = encoding[(int)(timestamp & 0x1F)];
            timestamp >>= 5;
        }

        // Encode 80-bit randomness (16 chars)
        int bitPos = 0;
        for (int i = 10; i < 26; i++)
        {
            int byteIndex = bitPos / 8;
            int bitOffset = bitPos % 8;
            int val;
            if (bitOffset <= 3)
            {
                val = (random[byteIndex] >> bitOffset) & 0x1F;
            }
            else
            {
                val = ((random[byteIndex] >> bitOffset) | (random[byteIndex + 1] << (8 - bitOffset))) & 0x1F;
            }
            result[i] = encoding[val];
            bitPos += 5;
        }

        return new string(result);
    }

    public sealed record SessionRow(
        string Id,
        long UserId,
        long UniverseId,
        long PlaceId,
        string? JobId,
        long StartedAt,
        long? EndedAt,
        long LastSeenAt,
        long UpdatedAt,
        int Deleted
    );

    public sealed record UniverseTotal(long UniverseId, long TotalSeconds);

    public sealed record GameRow(long UniverseId, string? Name, string? IconUrl, long FetchedAt);
}