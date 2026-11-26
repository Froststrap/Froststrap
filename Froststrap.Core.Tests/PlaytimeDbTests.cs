// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Services;
using Xunit;
using FluentAssertions;

namespace Froststrap.Tests;

[Collection("PlaytimeDbTests")]
public sealed class PlaytimeDbTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly PlaytimeDb _db;

    public PlaytimeDbTests()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"froststrap_playtime_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        _testDbPath = Path.Combine(testDir, "playtime.db");
        _db = new PlaytimeDb(testDir);
    }

    public void Dispose()
    {
        _db.Dispose();
        var testDir = Path.GetDirectoryName(_testDbPath);
        if (Directory.Exists(testDir))
            Directory.Delete(testDir, true);
    }

    [Fact]
    public async Task Migration_FromEmptyDb_CreatesSchemaV1()
    {
        // Act - just constructing and using the DB should trigger migration
        var session = await _db.OpenSessionAsync(123, 456, 789, "job-1");

        // Assert
        session.Should().NotBeNull();
        session.Id.Should().NotBeNullOrEmpty();
        session.UserId.Should().Be(123);
        session.UniverseId.Should().Be(456);
        session.PlaceId.Should().Be(789);
        session.JobId.Should().Be("job-1");
        session.StartedAt.Should().BeGreaterThan(0);
        session.EndedAt.Should().BeNull();
        session.LastSeenAt.Should().Be(session.StartedAt);
        session.Deleted.Should().Be(0);
    }

    [Fact]
    public async Task OpenSession_ThenTouch_UpdatesLastSeenAt()
    {
        // Arrange
        var session = await _db.OpenSessionAsync(123, 456, 789, "job-1");
        var originalLastSeen = session.LastSeenAt;

        // Delay to ensure timestamp difference (seconds precision)
        await Task.Delay(1100);

        // Act
        var touched = await _db.TouchSessionAsync(session.Id);

        // Assert
        touched.Should().BeTrue();

        var sessions = await _db.GetSessionsAsync(123, 456);
        sessions.Should().HaveCount(1);
        sessions[0].LastSeenAt.Should().BeGreaterThan(originalLastSeen);
        sessions[0].UpdatedAt.Should().BeGreaterThan(session.UpdatedAt);
    }

    [Fact]
    public async Task TouchSession_NonExistentId_ReturnsFalse()
    {
        var result = await _db.TouchSessionAsync("non-existent-id");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TouchSession_EndedSession_ReturnsFalse()
    {
        var session = await _db.OpenSessionAsync(123, 456, 789);
        var _ = await _db.EndSessionAsync(session.Id);

        var result = await _db.TouchSessionAsync(session.Id);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EndSession_SetsEndedAt_AndUpdatesLastSeenIfLater()
    {
        var session = await _db.OpenSessionAsync(123, 456, 789);
        var endTime = session.StartedAt + 3600; // 1 hour later

        // Delay to ensure UpdatedAt changes
        await Task.Delay(1100);

        var result = await _db.EndSessionAsync(session.Id, endTime);

        result.Success.Should().BeTrue();

        var sessions = await _db.GetSessionsAsync(123, 456);
        sessions.Should().HaveCount(1);
        sessions[0].EndedAt.Should().Be(endTime);
        sessions[0].LastSeenAt.Should().Be(endTime); // endTime > original last_seen_at
        sessions[0].UpdatedAt.Should().BeGreaterThan(session.UpdatedAt);
    }

    [Fact]
    public async Task EndSession_WithEndedBeforeLastSeen_ClampsEndedAtToLastSeen()
    {
        var session = await _db.OpenSessionAsync(123, 456, 789);
        await Task.Delay(1100);
        await _db.TouchSessionAsync(session.Id); // last_seen_at is now >= started_at

        // Try to end with a time BEFORE last_seen_at
        // Use a value less than last_seen_at (which is >= started_at after Touch)
        var earlyEndTime = session.StartedAt - 100; // definitely earlier than last_seen_at
        var result = await _db.EndSessionAsync(session.Id, earlyEndTime);

        result.Success.Should().BeTrue();
        // EndedAt should be clamped to at least last_seen_at (which is >= started_at)
        result.EndedAt.Should().BeGreaterThanOrEqualTo(result.LastSeenAt - 1); // allow 1 sec clock diff
        result.EndedAt.Should().BeGreaterThanOrEqualTo(session.StartedAt);

        var sessions = await _db.GetSessionsAsync(123, 456);
        sessions[0].EndedAt.Should().Be(result.EndedAt);
        sessions[0].LastSeenAt.Should().Be(result.LastSeenAt);
    }

    [Fact]
    public async Task EndSession_WithNullEndedAt_UsesNow()
    {
        var session = await _db.OpenSessionAsync(123, 456, 789);
        var beforeEnd = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var _ = await _db.EndSessionAsync(session.Id);

        var afterEnd = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sessions = await _db.GetSessionsAsync(123, 456);

        sessions[0].EndedAt.Should().NotBeNull();
        sessions[0].EndedAt!.Value.Should().BeGreaterThanOrEqualTo(beforeEnd);
        sessions[0].EndedAt!.Value.Should().BeLessThanOrEqualTo(afterEnd);
    }

    [Fact]
    public async Task CloseOrphans_SetsEndedAtToLastSeenAt_ForOpenSessions()
    {
        var session1 = await _db.OpenSessionAsync(123, 456, 789);
        var session2 = await _db.OpenSessionAsync(123, 456, 790);
        var _ = await _db.EndSessionAsync(session1.Id); // close one

        // Delay to ensure timestamp difference for CloseOrphans
        await Task.Delay(1100);

        var count = await _db.CloseOrphansAsync();

        count.Should().Be(1);

        var sessions = await _db.GetSessionsAsync(123, 456);
        sessions.Should().HaveCount(2);

        var openSession = sessions.First(s => s.Id == session2.Id);
        openSession.EndedAt.Should().Be(openSession.LastSeenAt);
        openSession.UpdatedAt.Should().BeGreaterThan(session2.UpdatedAt);
    }

    [Fact]
    public async Task CloseOrphans_NoOpenSessions_ReturnsZero()
    {
        var session = await _db.OpenSessionAsync(123, 456, 789);
        var _ = await _db.EndSessionAsync(session.Id);

        var count = await _db.CloseOrphansAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetTotalsPerUniverse_SumsDurationCorrectly()
    {
        var userId = 123;
        var universe1 = 456;
        var universe2 = 789;

        // Session 1: 1 hour in universe1
        var s1 = await _db.OpenSessionAsync(userId, universe1, 100);
        var _ = await _db.EndSessionAsync(s1.Id, s1.StartedAt + 3600);

        // Session 2: 30 min in universe1
        var s2 = await _db.OpenSessionAsync(userId, universe1, 101);
        var __ = await _db.EndSessionAsync(s2.Id, s2.StartedAt + 1800);

        // Session 3: 2 hours in universe2
        var s3 = await _db.OpenSessionAsync(userId, universe2, 200);
        var ___ = await _db.EndSessionAsync(s3.Id, s3.StartedAt + 7200);

        // Session 4: open (should use last_seen_at)
        var s4 = await _db.OpenSessionAsync(userId, universe2, 201);
        await _db.TouchSessionAsync(s4.Id);
        var s4LastSeen = (await _db.GetSessionsAsync(userId, universe2)).First(x => x.Id == s4.Id).LastSeenAt;

        var totals = await _db.GetTotalsPerUniverseAsync(userId);

        totals.Should().HaveCount(2);

        var u1Total = totals.First(x => x.UniverseId == universe1);
        u1Total.TotalSeconds.Should().Be(3600 + 1800);

        var u2Total = totals.First(x => x.UniverseId == universe2);
        u2Total.TotalSeconds.Should().BeGreaterThanOrEqualTo(7200 + (s4LastSeen - s4.StartedAt));
    }

    [Fact]
    public async Task GetTotalsPerUniverse_ExcludesDeletedSessions()
    {
        var userId = 123;
        var universeId = 456;

        var s1 = await _db.OpenSessionAsync(userId, universeId, 100);
        var _ = await _db.EndSessionAsync(s1.Id, s1.StartedAt + 3600);

        var s2 = await _db.OpenSessionAsync(userId, universeId, 101);
        var __ = await _db.EndSessionAsync(s2.Id, s2.StartedAt + 1800);
        await _db.DeleteAllForUserAsync(userId); // soft delete all

        var totals = await _db.GetTotalsPerUniverseAsync(userId);
        totals.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSessions_ReturnsSessionsForUserAndUniverse()
    {
        var userId = 123;
        var universe1 = 456;
        var universe2 = 789;

        await _db.OpenSessionAsync(userId, universe1, 100);
        await _db.OpenSessionAsync(userId, universe1, 101);
        await _db.OpenSessionAsync(userId, universe2, 200);

        var sessions = await _db.GetSessionsAsync(userId, universe1);

        sessions.Should().HaveCount(2);
        sessions.All(s => s.UserId == userId && s.UniverseId == universe1).Should().BeTrue();
    }

    [Fact]
    public async Task GetSessions_OrdersByStartedAtDescending()
    {
        var userId = 123;
        var universeId = 456;

        var s1 = await _db.OpenSessionAsync(userId, universeId, 100);
        await Task.Delay(1100);
        var s2 = await _db.OpenSessionAsync(userId, universeId, 101);
        await Task.Delay(1100);
        var s3 = await _db.OpenSessionAsync(userId, universeId, 102);

        var sessions = await _db.GetSessionsAsync(userId, universeId);

        sessions.Should().HaveCount(3);
        sessions[0].Id.Should().Be(s3.Id);
        sessions[1].Id.Should().Be(s2.Id);
        sessions[2].Id.Should().Be(s1.Id);
    }

    [Fact]
    public async Task GetSessions_ExcludesDeleted()
    {
        var userId = 123;
        var universeId = 456;

        var s1 = await _db.OpenSessionAsync(userId, universeId, 100);
        var s2 = await _db.OpenSessionAsync(userId, universeId, 101);
        await _db.DeleteAllForUserAsync(userId);

        var sessions = await _db.GetSessionsAsync(userId, universeId);
        sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAllForUser_SoftDeletesAllSessions()
    {
        var userId = 123;
        var universeId = 456;

        var s1 = await _db.OpenSessionAsync(userId, universeId, 100);
        var s2 = await _db.OpenSessionAsync(userId, universeId, 101);

        var count = await _db.DeleteAllForUserAsync(userId);

        count.Should().Be(2);

        var sessions = await _db.GetSessionsAsync(userId, universeId);
        sessions.Should().BeEmpty();

        // Verify soft delete in DB by checking totals (which filter deleted=0)
        var totals = await _db.GetTotalsPerUniverseAsync(userId);
        totals.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertGame_And_GetGame_Works()
    {
        var universeId = 456;

        await _db.UpsertGameAsync(universeId, "Test Game", "https://example.com/icon.png");

        var game = await _db.GetGameAsync(universeId);

        game.Should().NotBeNull();
        game!.UniverseId.Should().Be(universeId);
        game.Name.Should().Be("Test Game");
        game.IconUrl.Should().Be("https://example.com/icon.png");
        game.FetchedAt.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpsertGame_UpdatesExisting()
    {
        var universeId = 456;

        await _db.UpsertGameAsync(universeId, "Original", "https://orig.com/icon.png");
        await Task.Delay(1100);
        await _db.UpsertGameAsync(universeId, "Updated", "https://updated.com/icon.png");

        var game = await _db.GetGameAsync(universeId);

        game!.Name.Should().Be("Updated");
        game.IconUrl.Should().Be("https://updated.com/icon.png");
    }

    [Fact]
    public async Task UpsertGame_NullNameOrIcon_DoesNotOverwriteExisting()
    {
        var universeId = 456;

        await _db.UpsertGameAsync(universeId, "Original", "https://orig.com/icon.png");
        await _db.UpsertGameAsync(universeId, null, null); // pass nulls

        var game = await _db.GetGameAsync(universeId);

        game!.Name.Should().Be("Original");
        game.IconUrl.Should().Be("https://orig.com/icon.png");
    }

    [Fact]
    public async Task GetGame_NonExistent_ReturnsNull()
    {
        var game = await _db.GetGameAsync(999999);
        game.Should().BeNull();
    }

    [Fact]
    public async Task MultipleDbInstances_SamePath_ShareData()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"froststrap_playtime_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        var db1 = new PlaytimeDb(testDir);
        var db2 = new PlaytimeDb(testDir);

        try
        {
            var session = await db1.OpenSessionAsync(123, 456, 789);

            var sessions = await db2.GetSessionsAsync(123, 456);
            sessions.Should().HaveCount(1);
            sessions[0].Id.Should().Be(session.Id);
        }
        finally
        {
            db1.Dispose();
            db2.Dispose();
            Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public async Task EndSession_ClampsEndedAtAtLeastStartedAt()
    {
        var session = await _db.OpenSessionAsync(123, 456, 789);

        // Try to end BEFORE started_at (should clamp to started_at)
        var result = await _db.EndSessionAsync(session.Id, session.StartedAt - 100);
        result.Success.Should().BeTrue();
        result.EndedAt.Should().Be(session.StartedAt);

        var sessions = await _db.GetSessionsAsync(123, 456);
        sessions[0].EndedAt.Should().Be(session.StartedAt);
    }
}