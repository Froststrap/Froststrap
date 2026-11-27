// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Froststrap.Services;

public enum TimeRange
{
    Last7Days,
    Last30Days,
    Last90Days,
    AllTime
}

public enum ChartType
{
    Column,
    Bar,
    Pie,
    Histogram
}

public sealed record SummaryResult(
    long TotalSeconds,
    int DistinctGames,
    int SessionCount,
    long AverageSessionSeconds,
    long LongestSessionSeconds
);

public sealed record GameTotalResult(
    long UniverseId,
    long TotalSeconds,
    int SessionCount,
    long LastPlayedAt
);

public sealed record TrendBucketResult(
    DateTime BucketStartUtc,
    long Seconds
);

public sealed record HistogramBinResult(
    string Label,
    int Count
);

internal sealed class PlaytimeStats : IDisposable
{
    private readonly string _dbPath;
    private readonly SemaphoreSlim _readerLock = new(1, 1);
    private SqliteConnection? _connection;
    private bool _disposed;

    public PlaytimeStats(string? customDataRoot = null)
    {
        var dataRoot = customDataRoot ?? Paths.DataRoot;
        _dbPath = Path.Combine(dataRoot, "playtime.db");
    }

    private static (long rangeStart, long rangeEnd) GetRangeBounds(TimeRange range, long now)
    {
        return range switch
        {
            TimeRange.Last7Days => (now - 7 * 24 * 60 * 60, now),
            TimeRange.Last30Days => (now - 30 * 24 * 60 * 60, now),
            TimeRange.Last90Days => (now - 90 * 24 * 60 * 60, now),
            TimeRange.AllTime => (0, now),
            _ => (0, now)
        };
    }

    private async Task<List<SessionRow>> GetSessionsInRangeAsync(long userId, long rangeStart, long rangeEnd, CancellationToken ct)
    {
        var conn = await GetConnectionAsync();
        if (conn == null)
            return [];

        var results = new List<SessionRow>();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, user_id, universe_id, place_id, job_id, started_at, ended_at, last_seen_at, updated_at, deleted
            FROM sessions
            WHERE user_id = $userId AND deleted = 0
              AND started_at <= $rangeEnd
              AND COALESCE(ended_at, last_seen_at) >= $rangeStart
            ORDER BY started_at;
            """;
        cmd.Parameters.AddWithValue("$userId", userId);
        cmd.Parameters.AddWithValue("$rangeStart", rangeStart);
        cmd.Parameters.AddWithValue("$rangeEnd", rangeEnd);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SessionRow(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                await reader.IsDBNullAsync(4, ct) ? null : reader.GetString(4),
                reader.GetInt64(5),
                await reader.IsDBNullAsync(6, ct) ? null : reader.GetInt64(6),
                reader.GetInt64(7),
                reader.GetInt64(8),
                reader.GetInt32(9)
            ));
        }

        return results;
    }

    public async Task<SummaryResult> GetSummaryAsync(long userId, TimeRange range, long now, TimeZoneInfo tz, CancellationToken ct = default)
    {
        var (rangeStart, rangeEnd) = GetRangeBounds(range, now);
        var sessions = await GetSessionsInRangeAsync(userId, rangeStart, rangeEnd, ct);

        if (sessions.Count == 0)
            return new SummaryResult(0, 0, 0, 0, 0);

        long totalSeconds = 0;
        var universeIds = new HashSet<long>();
        long longestSession = 0;

        foreach (var s in sessions)
        {
            var sessionEnd = s.EndedAt ?? s.LastSeenAt;
            var clippedStart = Math.Max(s.StartedAt, rangeStart);
            var clippedEnd = Math.Min(sessionEnd, rangeEnd);
            var duration = clippedEnd - clippedStart;
            if (duration > 0)
            {
                totalSeconds += duration;
                longestSession = Math.Max(longestSession, duration);
            }
            universeIds.Add(s.UniverseId);
        }

        var avgSession = sessions.Count > 0 ? totalSeconds / sessions.Count : 0;

        return new SummaryResult(
            totalSeconds,
            universeIds.Count,
            sessions.Count,
            avgSession,
            longestSession
        );
    }

    public async Task<List<GameTotalResult>> GetPerGameTotalsAsync(long userId, TimeRange range, long now, TimeZoneInfo tz, CancellationToken ct = default)
    {
        var (rangeStart, rangeEnd) = GetRangeBounds(range, now);
        var sessions = await GetSessionsInRangeAsync(userId, rangeStart, rangeEnd, ct);

        var dict = new Dictionary<long, (long TotalSeconds, int Count, long LastPlayed)>();

        foreach (var s in sessions)
        {
            var sessionEnd = s.EndedAt ?? s.LastSeenAt;
            var clippedStart = Math.Max(s.StartedAt, rangeStart);
            var clippedEnd = Math.Min(sessionEnd, rangeEnd);
            var duration = clippedEnd - clippedStart;
            if (duration <= 0) continue;

            if (!dict.TryGetValue(s.UniverseId, out var existing))
            {
                dict[s.UniverseId] = (duration, 1, s.StartedAt);
            }
            else
            {
                dict[s.UniverseId] = (existing.TotalSeconds + duration, existing.Count + 1, Math.Max(existing.LastPlayed, s.StartedAt));
            }
        }

        var results = dict
            .Select(kvp => new GameTotalResult(kvp.Key, kvp.Value.TotalSeconds, kvp.Value.Count, kvp.Value.LastPlayed))
            .OrderByDescending(r => r.TotalSeconds)
            .ToList();

        return results;
    }

    public static List<GameTotalResult> TopNWithOther(List<GameTotalResult> totals, int n)
    {
        if (totals.Count <= n) return totals;

        var top = totals.Take(n).ToList();
        var otherSeconds = totals.Skip(n).Sum(x => x.TotalSeconds);
        var otherCount = totals.Skip(n).Sum(x => x.SessionCount);
        var otherLastPlayed = totals.Skip(n).Max(x => x.LastPlayedAt);

        top.Add(new GameTotalResult(0, otherSeconds, otherCount, otherLastPlayed));
        return top;
    }

    public async Task<List<TrendBucketResult>> GetTrendAsync(long userId, TimeRange range, long now, TimeZoneInfo tz, CancellationToken ct = default)
    {
        var (rangeStart, rangeEnd) = GetRangeBounds(range, now);
        var sessions = await GetSessionsInRangeAsync(userId, rangeStart, rangeEnd, ct);

        var buckets = new List<(DateTime StartUtc, DateTime EndUtc)>();

        if (range == TimeRange.Last7Days || range == TimeRange.Last30Days)
        {
            // Day buckets - exactly N days, end exclusive
            var startLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeSeconds(rangeStart).DateTime, tz).Date;
            var endLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeSeconds(rangeEnd).DateTime, tz).Date;
            var expectedDays = range == TimeRange.Last7Days ? 7 : 30;

            for (int i = 0; i < expectedDays; i++)
            {
                var bucketStartLocal = startLocal.AddDays(i);
                var bucketEndLocal = bucketStartLocal.AddDays(1);
                var bucketStartUtc = TimeZoneInfo.ConvertTimeToUtc(bucketStartLocal, tz);
                var bucketEndUtc = TimeZoneInfo.ConvertTimeToUtc(bucketEndLocal, tz);
                buckets.Add((bucketStartUtc, bucketEndUtc));
            }
        }
        else
        {
            // Week buckets (Monday start)
            var startLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeSeconds(rangeStart).DateTime, tz).Date;
            var endLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeSeconds(rangeEnd).DateTime, tz).Date;

            // Align to Monday
            while (startLocal.DayOfWeek != DayOfWeek.Monday)
                startLocal = startLocal.AddDays(-1);

            var current = startLocal;
            while (current <= endLocal)
            {
                var weekEnd = current.AddDays(7);
                var bucketStartUtc = TimeZoneInfo.ConvertTimeToUtc(current, tz);
                var bucketEndUtc = TimeZoneInfo.ConvertTimeToUtc(weekEnd, tz);
                buckets.Add((bucketStartUtc, bucketEndUtc));
                current = weekEnd;
            }
        }

        var bucketSeconds = new long[buckets.Count];

        foreach (var s in sessions)
        {
            var sessionEnd = s.EndedAt ?? s.LastSeenAt;
            var sessionStart = s.StartedAt;

            for (int i = 0; i < buckets.Count; i++)
            {
                var (bStart, bEnd) = buckets[i];
                var bStartSec = new DateTimeOffset(bStart).ToUnixTimeSeconds();
                var bEndSec = new DateTimeOffset(bEnd).ToUnixTimeSeconds();

                var overlapStart = Math.Max(sessionStart, bStartSec);
                var overlapEnd = Math.Min(sessionEnd, bEndSec);
                var overlap = overlapEnd - overlapStart;
                if (overlap > 0)
                    bucketSeconds[i] += overlap;
            }
        }

        var results = new List<TrendBucketResult>();
        for (int i = 0; i < buckets.Count; i++)
        {
            results.Add(new TrendBucketResult(buckets[i].StartUtc, bucketSeconds[i]));
        }

        return results;
    }

    public async Task<List<HistogramBinResult>> GetSessionLengthHistogramAsync(long userId, TimeRange range, long now, TimeZoneInfo tz, CancellationToken ct = default)
    {
        var (rangeStart, rangeEnd) = GetRangeBounds(range, now);
        var sessions = await GetSessionsInRangeAsync(userId, rangeStart, rangeEnd, ct);

        var bins = new Dictionary<string, int>
        {
            ["0-15m"] = 0,
            ["15-30m"] = 0,
            ["30-60m"] = 0,
            ["1-2h"] = 0,
            ["2h+"] = 0
        };

        foreach (var s in sessions)
        {
            var sessionEnd = s.EndedAt ?? s.LastSeenAt;
            var clippedStart = Math.Max(s.StartedAt, rangeStart);
            var clippedEnd = Math.Min(sessionEnd, rangeEnd);
            var duration = clippedEnd - clippedStart;
            if (duration <= 0) continue;

            string label;
            if (duration < 15 * 60)
                label = "0-15m";
            else if (duration < 30 * 60)
                label = "15-30m";
            else if (duration < 60 * 60)
                label = "30-60m";
            else if (duration < 2 * 60 * 60)
                label = "1-2h";
            else
                label = "2h+";

            bins[label]++;
        }

        return bins.Select(kvp => new HistogramBinResult(kvp.Key, kvp.Value)).ToList();
    }

    internal sealed record SessionRow(
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

    private async Task<SqliteConnection> GetConnectionAsync()
    {
        if (_connection is { State: System.Data.ConnectionState.Open })
            return _connection;

        await _readerLock.WaitAsync();
        try
        {
            if (_connection is { State: System.Data.ConnectionState.Open })
                return _connection;

            if (!File.Exists(_dbPath))
            {
                _readerLock.Release();
                return null!; // Caller handles null
            }

            _connection = new SqliteConnection($"Data Source={_dbPath};Pooling=False;Mode=ReadOnly;");
            await _connection.OpenAsync();

            using var pragma = _connection.CreateCommand();
            pragma.CommandText = "PRAGMA query_only=ON;";
            await pragma.ExecuteNonQueryAsync();

            return _connection;
        }
        catch
        {
            _readerLock.Release();
            throw;
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
            _readerLock.Dispose();
        }
    }
}