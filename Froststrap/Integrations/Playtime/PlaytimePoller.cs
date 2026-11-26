// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Integrations.AccountManager;
using Froststrap.Models.APIs.Roblox;
using Froststrap.Services;
using AccManager = Froststrap.Integrations.AccountManager.AccountManager;

namespace Froststrap.Integrations.Playtime;

internal sealed class PlaytimePoller : IDisposable
{
    private const int BaseIntervalSeconds = 30;
    private const int MaxBackoffSeconds = 300;

    private readonly PlaytimeDb _db;
    private readonly PlaytimeStateMachine _stateMachine;
    private readonly AccManager _accountManager;
    private readonly CancellationTokenSource _cts = new();
    private Task? _pollTask;
    private int _currentBackoffSeconds = BaseIntervalSeconds;
    private bool _disposed;

    public PlaytimePoller(PlaytimeDb db, PlaytimeStateMachine stateMachine, AccManager accountManager)
    {
        _db = db;
        _stateMachine = stateMachine;
        _accountManager = accountManager;
    }

    public void Start()
    {
        if (_pollTask != null)
            return;

        _cts.Token.ThrowIfCancellationRequested();
        _currentBackoffSeconds = BaseIntervalSeconds;
        _pollTask = PollLoopAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_pollTask == null)
            return;

        _cts.Cancel();
        try
        {
            await _pollTask;
        }
        catch (OperationCanceledException) { }
        _pollTask = null;
    }

    public async Task EndAllSessionsAsync(long userId, long now)
    {
        var actions = new List<PlaytimeStateMachine.Action>();
        _stateMachine.EndAllSessions(userId, now, actions);

        foreach (var action in actions)
        {
            if (action.Type == PlaytimeStateMachine.ActionType.EndSession && action.SessionId != null)
            {
                await _db.EndSessionAsync(action.SessionId, ct: CancellationToken.None);
            }
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(ct);
                _currentBackoffSeconds = BaseIntervalSeconds; // Reset on success
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _currentBackoffSeconds = Math.Min(_currentBackoffSeconds * 2, MaxBackoffSeconds);
                App.Logger.Warn($"Playtime poller rate limited, backing off to {_currentBackoffSeconds}s");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Playtime poller error");
                _currentBackoffSeconds = Math.Min(_currentBackoffSeconds * 2, MaxBackoffSeconds);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_currentBackoffSeconds), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        if (!App.Settings.Prop.AllowCookieAccess)
            return;

        var accounts = _accountManager.Accounts.Where(a => !string.IsNullOrEmpty(_accountManager.GetRoblosecurityForUser(a.UserId))).ToList();
        if (accounts.Count == 0)
            return;

        // Use active account's cookie, or first available
        var cookieAccount = _accountManager.ActiveAccount ?? accounts[0];
        var cookie = _accountManager.GetRoblosecurityForUser(cookieAccount.UserId);
        if (string.IsNullOrEmpty(cookie))
            return;

        var userIds = accounts.Select(a => a.UserId).ToArray();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var presences = await RobloxApiService.GetUserPresencesWithCookieAsync(cookie, userIds);

        var presenceByUserId = presences.ToDictionary(p => p.UserId);

        foreach (var account in accounts)
        {
            if (ct.IsCancellationRequested)
                break;

            presenceByUserId.TryGetValue(account.UserId, out var presence);
            var actions = _stateMachine.Update(account.UserId, presence, now);

            foreach (var action in actions)
            {
                await ApplyActionAsync(action, ct);
            }
        }
    }

    private async Task ApplyActionAsync(PlaytimeStateMachine.Action action, CancellationToken ct)
    {
        switch (action.Type)
        {
            case PlaytimeStateMachine.ActionType.OpenSession:
                var session = await _db.OpenSessionAsync(action.UserId, action.UniverseId, action.PlaceId, ct: ct);
                _stateMachine.RegisterSessionOpened(action.UserId, session.Id, action.UniverseId, action.PlaceId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                break;

            case PlaytimeStateMachine.ActionType.TouchSession:
                if (action.SessionId != null)
                    await _db.TouchSessionAsync(action.SessionId, ct);
                break;

            case PlaytimeStateMachine.ActionType.EndSession:
                if (action.SessionId != null)
                    await _db.EndSessionAsync(action.SessionId, ct: ct);
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        try { _pollTask?.Wait(1000); } catch { }
        _cts.Dispose();
    }
}