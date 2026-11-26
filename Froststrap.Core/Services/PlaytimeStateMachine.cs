// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Models.APIs.Roblox;
using System.Diagnostics;

namespace Froststrap.Services;

internal sealed class PlaytimeStateMachine
{
    private sealed record AccountState
    {
        public string? SessionId { get; set; }
        public long UniverseId { get; set; }
        public long PlaceId { get; set; }
        public int ConsecutiveNotInGame { get; set; }
        public long LastSeenAt { get; set; }
        public bool HasMissingIdsLogged { get; set; }
    }

    private readonly Dictionary<long, AccountState> _states = new();

    public enum ActionType
    {
        None,
        OpenSession,
        TouchSession,
        EndSession,
        EndSessionAndOpenNew
    }

    public sealed record Action(
        ActionType Type,
        long UserId,
        long UniverseId,
        long PlaceId,
        string? SessionId
    );

    public PlaytimeStateMachine()
    {
    }

    public List<Action> Update(long userId, UserPresence? presence, long now)
    {
        if (!_states.TryGetValue(userId, out var state))
        {
            state = new AccountState();
            _states[userId] = state;
        }

        var actions = new List<Action>();

        if (presence == null)
        {
            actions.AddRange(HandleNotInGame(userId, state, now));
            return actions;
        }

        if (presence.UserPresenceType == 2) // InGame
        {
            if (presence.UniverseId is not > 0 || presence.PlaceId is not > 0)
            {
                if (!state.HasMissingIdsLogged)
                {
                    Debug.WriteLine($"Playtime: user {userId} InGame but missing universeId/placeId; ignoring until present");
                    state.HasMissingIdsLogged = true;
                }
                return actions;
            }

            state.HasMissingIdsLogged = false;

            var currentUniverseId = presence.UniverseId.Value;
            var currentPlaceId = presence.PlaceId.Value;

            if (state.SessionId != null && state.UniverseId == currentUniverseId)
            {
                // Same universe - touch session (placeId change = teleport, same session)
                state.LastSeenAt = now;
                state.PlaceId = currentPlaceId; // update to current place
                actions.Add(new Action(ActionType.TouchSession, userId, currentUniverseId, currentPlaceId, state.SessionId));
            }
            else if (state.SessionId != null)
            {
                // Different universe - end old, open new
                actions.Add(new Action(ActionType.EndSession, userId, state.UniverseId, state.PlaceId, state.SessionId));
                state.SessionId = null;
                state.UniverseId = currentUniverseId;
                state.PlaceId = currentPlaceId;
                state.LastSeenAt = now;
                state.ConsecutiveNotInGame = 0;
                actions.Add(new Action(ActionType.OpenSession, userId, currentUniverseId, currentPlaceId, null));
            }
            else
            {
                // No open session - open new
                state.UniverseId = currentUniverseId;
                state.PlaceId = currentPlaceId;
                state.LastSeenAt = now;
                state.ConsecutiveNotInGame = 0;
                actions.Add(new Action(ActionType.OpenSession, userId, currentUniverseId, currentPlaceId, null));
            }
        }
        else
        {
            // Not in game (Online=1, Studio=3, Offline=0)
            actions.AddRange(HandleNotInGame(userId, state, now));
        }

        return actions;
    }

    private List<Action> HandleNotInGame(long userId, AccountState state, long now)
    {
        var actions = new List<Action>();

        if (state.SessionId != null)
        {
            state.ConsecutiveNotInGame++;
            if (state.ConsecutiveNotInGame >= 2)
            {
                // End session at last_seen_at (NOT detection time)
                actions.Add(new Action(ActionType.EndSession, userId, state.UniverseId, state.PlaceId, state.SessionId));
                state.SessionId = null;
                state.ConsecutiveNotInGame = 0;
            }
        }

        return actions;
    }

    public void RegisterSessionOpened(long userId, string sessionId, long universeId, long placeId, long now)
    {
        if (!_states.TryGetValue(userId, out var state))
        {
            state = new AccountState();
            _states[userId] = state;
        }
        state.SessionId = sessionId;
        state.UniverseId = universeId;
        state.PlaceId = placeId;
        state.LastSeenAt = now;
        state.ConsecutiveNotInGame = 0;
    }

    public void EndAllSessions(long userId, long now, List<Action> actions)
    {
        if (_states.TryGetValue(userId, out var state) && state.SessionId != null)
        {
            actions.Add(new Action(ActionType.EndSession, userId, state.UniverseId, state.PlaceId, state.SessionId));
            state.SessionId = null;
        }
    }

    public void RemoveAccount(long userId)
    {
        _states.Remove(userId);
    }
}