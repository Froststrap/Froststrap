// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Models.APIs.Roblox;
using Froststrap.Services;
using Xunit;
using FluentAssertions;

namespace Froststrap.Tests;

public sealed class PlaytimeStateMachineTests
{
    private readonly PlaytimeStateMachine _machine;

    public PlaytimeStateMachineTests()
    {
        _machine = new PlaytimeStateMachine();
    }

    [Fact]
    public void Update_JoinGame_CreatesOpenSessionAction()
    {
        var now = 1000L;
        var presence = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 2, // InGame
            UniverseId = 456,
            PlaceId = 789
        };

        var actions = _machine.Update(123, presence, now);

        actions.Should().HaveCount(1);
        actions[0].Type.Should().Be(PlaytimeStateMachine.ActionType.OpenSession);
        actions[0].UserId.Should().Be(123);
        actions[0].UniverseId.Should().Be(456);
        actions[0].PlaceId.Should().Be(789);
        actions[0].SessionId.Should().BeNull();
    }

    [Fact]
    public void Update_SameUniverse_TouchesSession()
    {
        var now = 1000L;
        var presence = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 2,
            UniverseId = 456,
            PlaceId = 789
        };

        var actions1 = _machine.Update(123, presence, now);
        actions1.Should().HaveCount(1);
        actions1[0].Type.Should().Be(PlaytimeStateMachine.ActionType.OpenSession);

        // Simulate session opened
        _machine.RegisterSessionOpened(123, "session-1", 456, 789, now);

        // Second poll - same universe, different place (teleport)
        var presence2 = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 2,
            UniverseId = 456,
            PlaceId = 999 // different place, same universe
        };

        var actions2 = _machine.Update(123, presence2, now + 30);

        actions2.Should().HaveCount(1);
        actions2[0].Type.Should().Be(PlaytimeStateMachine.ActionType.TouchSession);
        actions2[0].SessionId.Should().Be("session-1");
        actions2[0].PlaceId.Should().Be(999); // updated to current place
    }

    [Fact]
    public void Update_DifferentUniverse_EndsOldOpensNew()
    {
        var now = 1000L;
        var presence1 = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 2,
            UniverseId = 456,
            PlaceId = 789
        };

        var actions1 = _machine.Update(123, presence1, now);
        actions1.Should().HaveCount(1);
        _machine.RegisterSessionOpened(123, "session-1", 456, 789, now);

        // Switch to different universe
        var presence2 = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 2,
            UniverseId = 999,
            PlaceId = 888
        };

        var actions2 = _machine.Update(123, presence2, now + 30);

        actions2.Should().HaveCount(2);
        actions2[0].Type.Should().Be(PlaytimeStateMachine.ActionType.EndSession);
        actions2[0].SessionId.Should().Be("session-1");
        actions2[1].Type.Should().Be(PlaytimeStateMachine.ActionType.OpenSession);
        actions2[1].UniverseId.Should().Be(999);
        actions2[1].PlaceId.Should().Be(888);
    }

    [Fact]
    public void Update_MissingUniverseOrPlace_IgnoresAndLogsOnce()
    {
        var now = 1000L;
        var presence = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 2, // InGame
            UniverseId = null,
            PlaceId = 789
        };

        var actions1 = _machine.Update(123, presence, now);
        actions1.Should().BeEmpty();

        // Second time - should still ignore but not log again
        var actions2 = _machine.Update(123, presence, now + 30);
        actions2.Should().BeEmpty();
    }

    [Fact]
    public void Update_NotInGame_FirstMiss_NoAction()
    {
        var now = 1000L;
        var presence = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 2,
            UniverseId = 456,
            PlaceId = 789
        };

        var actions1 = _machine.Update(123, presence, now);
        _machine.RegisterSessionOpened(123, "session-1", 456, 789, now);

        // First miss - user goes offline
        var presenceOffline = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 0, // Offline
            UniverseId = null,
            PlaceId = null
        };

        var actions2 = _machine.Update(123, presenceOffline, now + 30);
        actions2.Should().BeEmpty(); // First miss, no action yet
    }

    [Fact]
    public void Update_NotInGame_SecondMiss_EndsSessionAtLastSeenAt()
    {
        var now = 1000L;
        var presence = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 2,
            UniverseId = 456,
            PlaceId = 789
        };

        var actions1 = _machine.Update(123, presence, now);
        _machine.RegisterSessionOpened(123, "session-1", 456, 789, now);

        // Touch to update last_seen_at
        var presence2 = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 2,
            UniverseId = 456,
            PlaceId = 789
        };
        var actions2 = _machine.Update(123, presence2, now + 60); // last_seen_at = 1060
        actions2[0].Type.Should().Be(PlaytimeStateMachine.ActionType.TouchSession);

        // First miss
        var presenceOffline = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 0,
            UniverseId = null,
            PlaceId = null
        };
        var actions3 = _machine.Update(123, presenceOffline, now + 90);
        actions3.Should().BeEmpty();

        // Second miss - should end session at last_seen_at (1060), NOT detection time (1090)
        var actions4 = _machine.Update(123, presenceOffline, now + 120);
        actions4.Should().HaveCount(1);
        actions4[0].Type.Should().Be(PlaytimeStateMachine.ActionType.EndSession);
        actions4[0].SessionId.Should().Be("session-1");
    }

    [Fact]
    public void Update_StudioIgnored()
    {
        var now = 1000L;
        var presence = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 3, // In Studio
            UniverseId = 456,
            PlaceId = 789
        };

        var actions = _machine.Update(123, presence, now);
        actions.Should().BeEmpty();
    }

    [Fact]
    public void Update_OnlineIgnored()
    {
        var now = 1000L;
        var presence = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 1, // Online
            UniverseId = 456,
            PlaceId = 789
        };

        var actions = _machine.Update(123, presence, now);
        actions.Should().BeEmpty();
    }

    [Fact]
    public void Update_Flap_OneMissThenRecovery_KeepsSessionOpen()
    {
        var now = 1000L;
        var presence = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 2,
            UniverseId = 456,
            PlaceId = 789
        };

        var actions1 = _machine.Update(123, presence, now);
        _machine.RegisterSessionOpened(123, "session-1", 456, 789, now);

        // First miss
        var presenceOffline = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 0,
            UniverseId = null,
            PlaceId = null
        };
        var actions2 = _machine.Update(123, presenceOffline, now + 30);
        actions2.Should().BeEmpty();

        // Recovery - back in game same universe
        var presence3 = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 2,
            UniverseId = 456,
            PlaceId = 789
        };
        var actions3 = _machine.Update(123, presence3, now + 60);

        actions3.Should().HaveCount(1);
        actions3[0].Type.Should().Be(PlaytimeStateMachine.ActionType.TouchSession);
        actions3[0].SessionId.Should().Be("session-1");
    }

    [Fact]
    public void EndAllSessions_EndsOpenSession()
    {
        var now = 1000L;
        var presence = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 2,
            UniverseId = 456,
            PlaceId = 789
        };

        _machine.Update(123, presence, now);
        _machine.RegisterSessionOpened(123, "session-1", 456, 789, now);

        var actions = new List<PlaytimeStateMachine.Action>();
        _machine.EndAllSessions(123, now + 100, actions);

        actions.Should().HaveCount(1);
        actions[0].Type.Should().Be(PlaytimeStateMachine.ActionType.EndSession);
        actions[0].SessionId.Should().Be("session-1");
    }

    [Fact]
    public void EndAllSessions_NoOpenSession_NoAction()
    {
        var actions = new List<PlaytimeStateMachine.Action>();
        _machine.EndAllSessions(123, 1000, actions);
        actions.Should().BeEmpty();
    }

    [Fact]
    public void RemoveAccount_ClearsState()
    {
        var now = 1000L;
        var presence = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 2,
            UniverseId = 456,
            PlaceId = 789
        };

        _machine.Update(123, presence, now);
        _machine.RegisterSessionOpened(123, "session-1", 456, 789, now);

        _machine.RemoveAccount(123);

        // New update should create fresh state
        var actions = _machine.Update(123, presence, now + 100);
        actions.Should().HaveCount(1);
        actions[0].Type.Should().Be(PlaytimeStateMachine.ActionType.OpenSession);
    }

    [Fact]
    public void Update_NullPresence_TreatedAsOffline()
    {
        var now = 1000L;
        var presence = new UserPresence
        {
            UserId = 123,
            UserPresenceType = 2,
            UniverseId = 456,
            PlaceId = 789
        };

        _machine.Update(123, presence, now);
        _machine.RegisterSessionOpened(123, "session-1", 456, 789, now);

        // Null presence = offline
        var actions = _machine.Update(123, null, now + 30);
        actions.Should().BeEmpty(); // First miss

        actions = _machine.Update(123, null, now + 60);
        actions.Should().HaveCount(1);
        actions[0].Type.Should().Be(PlaytimeStateMachine.ActionType.EndSession);
    }
}