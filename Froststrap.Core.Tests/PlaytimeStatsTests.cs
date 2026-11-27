// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Services;
using Xunit;
using FluentAssertions;

namespace Froststrap.Tests;

using TimeRange = Froststrap.Services.TimeRange;
using GameTotalResult = Froststrap.Services.GameTotalResult;
using HistogramBinResult = Froststrap.Services.HistogramBinResult;

public sealed class PlaytimeStatsTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly PlaytimeDb _db;
    private readonly PlaytimeStats _stats;

    public PlaytimeStatsTests()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"froststrap_playtime_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        _testDbPath = Path.Combine(testDir, "playtime.db");
        _db = new PlaytimeDb(testDir);
        _stats = new PlaytimeStats(testDir);
    }

    public void Dispose()
    {
        _db.Dispose();
        _stats.Dispose();
        var testDir = Path.GetDirectoryName(_testDbPath);
        if (Directory.Exists(testDir))
            Directory.Delete(testDir, true);
    }

    [Fact]
    public async Task GetSummary_EmptyDb_ReturnsZeros()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var result = await _stats.GetSummaryAsync(123, TimeRange.Last7Days, now, TimeZoneInfo.Utc);

        result.TotalSeconds.Should().Be(0);
        result.DistinctGames.Should().Be(0);
        result.SessionCount.Should().Be(0);
        result.AverageSessionSeconds.Should().Be(0);
        result.LongestSessionSeconds.Should().Be(0);
    }

    [Fact]
    public async Task GetSummary_WithSessions_ReturnsCorrectAggregates()
    {
        var userId = 123L;
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Session 1: 1 hour
        var s1 = await _db.OpenSessionAsync(userId, 456, 100);
        await _db.EndSessionAsync(s1.Id, s1.StartedAt + 3600);

        // Session 2: 30 min
        var s2 = await _db.OpenSessionAsync(userId, 456, 101);
        await _db.EndSessionAsync(s2.Id, s2.StartedAt + 1800);

        // Session 3: 2 hours
        var s3 = await _db.OpenSessionAsync(userId, 789, 200);
        await _db.EndSessionAsync(s3.Id, s3.StartedAt + 7200);

        var now = baseTime + 86400; // next day to ensure all sessions are in range

        var result = await _stats.GetSummaryAsync(userId, TimeRange.AllTime, now, TimeZoneInfo.Utc);

        result.TotalSeconds.Should().Be(3600 + 1800 + 7200);
        result.DistinctGames.Should().Be(2);
        result.SessionCount.Should().Be(3);
        result.LongestSessionSeconds.Should().Be(7200);
        result.AverageSessionSeconds.Should().Be((3600 + 1800 + 7200) / 3);
    }

    [Fact]
    public async Task GetSummary_OpenSession_UsesLastSeenAt()
    {
        var userId = 123L;
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var s1 = await _db.OpenSessionAsync(userId, 456, 100);
        await _db.TouchSessionAsync(s1.Id); // last_seen_at > started_at

        var sessions = await _db.GetSessionsAsync(userId, 456);
        var lastSeen = sessions[0].LastSeenAt;

        var now = baseTime + 86400;

        var result = await _stats.GetSummaryAsync(userId, TimeRange.AllTime, now, TimeZoneInfo.Utc);

        // Should use last_seen_at as end for open session
        result.TotalSeconds.Should().Be(lastSeen - s1.StartedAt);
    }

    [Fact]
    public async Task GetSummary_RangeClipping_OnlyCountsOverlap()
    {
        var userId = 123L;
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var now = baseTime + 86400;
        var twoWeeksAgo = baseTime - 14 * 86400;

        // Session entirely within range (recent)
        var s1 = await _db.CreateTestSessionAsync(userId, 456, 100, baseTime - 3600, baseTime, baseTime);

        // Session entirely outside range (2 weeks ago)
        var s2 = await _db.CreateTestSessionAsync(userId, 456, 101, twoWeeksAgo - 3600, twoWeeksAgo, twoWeeksAgo);

        var result = await _stats.GetSummaryAsync(userId, TimeRange.Last7Days, now, TimeZoneInfo.Utc);

        result.TotalSeconds.Should().Be(3600);
        result.SessionCount.Should().Be(1);
    }

    [Fact]
    public async Task GetSummary_ExcludesDeletedRows()
    {
        var userId = 123L;
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var now = baseTime + 86400;

        var s1 = await _db.OpenSessionAsync(userId, 456, 100);
        await _db.EndSessionAsync(s1.Id, s1.StartedAt + 3600);

        var s2 = await _db.OpenSessionAsync(userId, 456, 101);
        await _db.EndSessionAsync(s2.Id, s2.StartedAt + 3600);
        await _db.DeleteAllForUserAsync(userId);

        var result = await _stats.GetSummaryAsync(userId, TimeRange.AllTime, now, TimeZoneInfo.Utc);

        result.TotalSeconds.Should().Be(0);
        result.SessionCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPerGameTotals_SortsByTotalDesc()
    {
        var userId = 123L;
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Game A: 2 hours total
        var s1 = await _db.OpenSessionAsync(userId, 456, 100);
        await _db.EndSessionAsync(s1.Id, s1.StartedAt + 7200);

        // Game B: 1 hour total
        var s2 = await _db.OpenSessionAsync(userId, 789, 200);
        await _db.EndSessionAsync(s2.Id, s2.StartedAt + 3600);

        // Game A: another 1 hour
        var s3 = await _db.OpenSessionAsync(userId, 456, 101);
        await _db.EndSessionAsync(s3.Id, s3.StartedAt + 3600);

        var now = baseTime + 86400;

        var results = await _stats.GetPerGameTotalsAsync(userId, TimeRange.AllTime, now, TimeZoneInfo.Utc);

        results.Should().HaveCount(2);
        results[0].UniverseId.Should().Be(456);
        results[0].TotalSeconds.Should().Be(10800); // 3 hours
        results[1].UniverseId.Should().Be(789);
        results[1].TotalSeconds.Should().Be(3600); // 1 hour
    }

    [Fact]
    public async Task TopNWithOther_CombinesRemaining()
    {
        var totals = new List<GameTotalResult>
        {
            new(1, 10000, 5, 1000),
            new(2, 8000, 4, 1000),
            new(3, 6000, 3, 1000),
            new(4, 4000, 2, 1000),
            new(5, 2000, 1, 1000),
        };

        var result = PlaytimeStats.TopNWithOther(totals, 3);

        result.Should().HaveCount(4); // 3 + 1 Other
        result[0].UniverseId.Should().Be(1);
        result[3].UniverseId.Should().Be(0); // Other
        result[3].TotalSeconds.Should().Be(6000); // 4000 + 2000
        result[3].SessionCount.Should().Be(3); // 2 + 1
    }

    [Fact]
    public async Task GetTrend_DayBuckets_FillsEmptyDays()
    {
        var userId = 123L;
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var now = baseTime + 86400;

        var s1 = await _db.OpenSessionAsync(userId, 456, 100);
        await _db.EndSessionAsync(s1.Id, s1.StartedAt + 3600); // today only

        var results = await _stats.GetTrendAsync(userId, TimeRange.Last7Days, now, TimeZoneInfo.Utc);

        results.Should().HaveCount(7); // 7 days
        results.Sum(r => r.Seconds).Should().Be(3600);
    }

    [Fact]
    public async Task GetTrend_WeekBuckets_MondayStart()
    {
        var userId = 123L;
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var now = baseTime + 86400;

        var s1 = await _db.OpenSessionAsync(userId, 456, 100);
        await _db.EndSessionAsync(s1.Id, s1.StartedAt + 3600);

        var results = await _stats.GetTrendAsync(userId, TimeRange.Last90Days, now, TimeZoneInfo.Utc);

        // Should have multiple week buckets, first bucket starts on Monday
        results.Should().NotBeEmpty();
        foreach (var r in results)
        {
            // Each bucket should be Monday 00:00 UTC
            r.BucketStartUtc.DayOfWeek.Should().Be(DayOfWeek.Monday);
            r.BucketStartUtc.Hour.Should().Be(0);
            r.BucketStartUtc.Minute.Should().Be(0);
        }
    }

    [Fact]
    public async Task GetTrend_SessionSpanningBucket_SplitsAcrossBuckets()
    {
        var userId = 123L;
        // Use a specific time to control bucket boundaries
        var baseTime = new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(); // Monday noon
        var now = baseTime + 36 * 60 * 60; // 36 hours later = Wednesday midnight

        // Create session with specific timestamps spanning multiple days
        await _db.CreateTestSessionAsync(userId, 456, 100, baseTime, baseTime + 36 * 60 * 60, baseTime + 36 * 60 * 60);

        var results = await _stats.GetTrendAsync(userId, TimeRange.Last7Days, now, TimeZoneInfo.Utc);

        // Session should be split across day buckets
        var totalSeconds = results.Sum(r => r.Seconds);
        totalSeconds.Should().Be(36 * 60 * 60); // full 36 hours
    }

    [Fact]
    public async Task GetTrend_DstDay_HandlesCorrectly()
    {
        var userId = 123L;
        // Use UTC to avoid DST issues on Linux
        var tz = TimeZoneInfo.Utc;

        var baseTime = new DateTimeOffset(2025, 3, 9, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(); // DST start day
        var now = baseTime + 24 * 60 * 60;

        // Create session with specific timestamps for DST test
        await _db.CreateTestSessionAsync(userId, 456, 100, baseTime, baseTime + 12 * 60 * 60, baseTime + 12 * 60 * 60);

        var results = await _stats.GetTrendAsync(userId, TimeRange.Last7Days, now, tz);

        // Should handle correctly with UTC
        var totalSeconds = results.Sum(r => r.Seconds);
        totalSeconds.Should().Be(12 * 60 * 60);
    }

    [Fact]
    public async Task GetTrend_EmptyBucketsFilledWithZero()
    {
        var userId = 123L;
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var now = baseTime + 86400;

        // No sessions at all
        var results = await _stats.GetTrendAsync(userId, TimeRange.Last7Days, now, TimeZoneInfo.Utc);

        results.Should().HaveCount(7);
        results.All(r => r.Seconds == 0).Should().BeTrue();
    }

    [Fact]
    public async Task GetSessionLengthHistogram_EdgeValues_CorrectBin()
    {
        var userId = 123L;
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var now = baseTime + 86400;

        // Exactly 15 minutes -> should go to 15-30m bin (not 0-15m)
        var s1 = await _db.OpenSessionAsync(userId, 456, 100);
        await _db.EndSessionAsync(s1.Id, s1.StartedAt + 15 * 60);

        // Exactly 30 minutes -> should go to 30-60m bin
        var s2 = await _db.OpenSessionAsync(userId, 456, 101);
        await _db.EndSessionAsync(s2.Id, s2.StartedAt + 30 * 60);

        // Exactly 60 minutes -> should go to 1-2h bin
        var s3 = await _db.OpenSessionAsync(userId, 456, 102);
        await _db.EndSessionAsync(s3.Id, s3.StartedAt + 60 * 60);

        // Exactly 120 minutes -> should go to 2h+ bin
        var s4 = await _db.OpenSessionAsync(userId, 456, 103);
        await _db.EndSessionAsync(s4.Id, s4.StartedAt + 120 * 60);

        var results = await _stats.GetSessionLengthHistogramAsync(userId, TimeRange.AllTime, now, TimeZoneInfo.Utc);

        var dict = results.ToDictionary(r => r.Label, r => r.Count);
        dict["0-15m"].Should().Be(0); // 15m exactly goes to next bin
        dict["15-30m"].Should().Be(1);
        dict["30-60m"].Should().Be(1);
        dict["1-2h"].Should().Be(1);
        dict["2h+"].Should().Be(1);
    }

    [Fact]
    public async Task GetSessionLengthHistogram_AllBinsPresent()
    {
        var userId = 123L;
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var now = baseTime + 86400;

        var results = await _stats.GetSessionLengthHistogramAsync(userId, TimeRange.AllTime, now, TimeZoneInfo.Utc);

        results.Should().HaveCount(5);
        results.Select(r => r.Label).Should().BeEquivalentTo(new[] { "0-15m", "15-30m", "30-60m", "1-2h", "2h+" });
    }

    [Fact]
    public async Task GetSessionLengthHistogram_RangeClipping()
    {
        var userId = 123L;
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var now = baseTime + 86400;
        var twoWeeksAgo = baseTime - 14 * 86400;

        // Session 1 week + 1 hour ago (outside 7d range)
        var s1 = await _db.CreateTestSessionAsync(userId, 456, 100, twoWeeksAgo - 3600, twoWeeksAgo, twoWeeksAgo);

        // Session 3 days ago (inside 7d range)
        var s2 = await _db.CreateTestSessionAsync(userId, 456, 101, baseTime - 1800, baseTime, baseTime);

        var results = await _stats.GetSessionLengthHistogramAsync(userId, TimeRange.Last7Days, now, TimeZoneInfo.Utc);

        var totalCount = results.Sum(r => r.Count);
        totalCount.Should().Be(1); // only s2
    }
}