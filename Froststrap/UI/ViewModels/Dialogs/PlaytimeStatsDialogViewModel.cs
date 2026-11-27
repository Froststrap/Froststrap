// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations.AccountManager;
using Froststrap.Localisation;
using Froststrap.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Dialogs;

internal sealed class PlaytimeStatsDialogViewModel : NotifyPropertyChangedViewModel, IDisposable
{
    private readonly AccountManager _accountManager;
    private CancellationTokenSource _loadCts = new();
    private int _loadSeq;
    private bool _disposed;

    public PlaytimeStatsDialogViewModel()
    {
        _accountManager = AccountManager.Shared;
        _accountManager.ActiveAccountChanged += OnActiveAccountChanged;

        Accounts = new ObservableCollection<AccountManagerAccount>(_accountManager.Accounts);
        Ranges = new ObservableCollection<PlaytimeStats.TimeRange>(Enum.GetValues<PlaytimeStats.TimeRange>());

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        _ = RefreshAsync();
    }

    public ObservableCollection<AccountManagerAccount> Accounts { get; }

    public AccountManagerAccount? SelectedAccount
    {
        get => _accountManager.ActiveAccount;
        set
        {
            if (value != _accountManager.ActiveAccount)
            {
                _accountManager.SetActiveAccount(value?.UserId);
            }
        }
    }

    public bool ShowAccountSelector => Accounts.Count > 1;

    public PlaytimeStats.TimeRange SelectedRange
    {
        get => _selectedRange;
        set
        {
            if (SetProperty(ref _selectedRange, value))
                _ = RefreshAsync();
        }
    }
    private PlaytimeStats.TimeRange _selectedRange = PlaytimeStats.TimeRange.Last30Days;

    public ObservableCollection<PlaytimeStats.TimeRange> Ranges { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }
    private bool _isLoading;

    public PlaytimeStats.SummaryResult? Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }
    private PlaytimeStats.SummaryResult? _summary;

    public ObservableCollection<GameTotalViewItem> GameTotals { get; } = [];

    public sealed record GameTotalViewItem(
        long UniverseId,
        long TotalSeconds,
        int SessionCount,
        long LastPlayedAt,
        string FormattedTime,
        string FormattedLastPlayed
    );

    public string EmptyMessage
    {
        get => _emptyMessage;
        private set => SetProperty(ref _emptyMessage, value);
    }
    private string _emptyMessage = string.Empty;

    public bool HasSummary => Summary != null;

    public string SummaryTotalTime => Summary?.TotalSeconds > 0 ? FormatDuration(Summary.TotalSeconds) : "—";
    public int SummaryGamesPlayed => Summary?.DistinctGames ?? 0;
    public int SummarySessions => Summary?.SessionCount ?? 0;
    public string SummaryAvgSession => Summary?.AverageSessionSeconds > 0 ? FormatDuration(Summary.AverageSessionSeconds) : "—";
    public string SummaryLongestSession => Summary?.LongestSessionSeconds > 0 ? FormatDuration(Summary.LongestSessionSeconds) : "—";

    public ICommand RefreshCommand { get; }

    public async Task RefreshAsync()
    {
        if (IsLoading) return;

        var seq = ++_loadSeq;
        _loadCts.Cancel();
        _loadCts = new CancellationTokenSource();

        IsLoading = true;
        Summary = null;
        GameTotals.Clear();
        EmptyMessage = string.Empty;

        try
        {
            var account = SelectedAccount;
            if (account == null)
            {
                EmptyMessage = Strings.Menu_Integrations_PlaytimeStats_NoAccount;
                return;
            }

            if (!App.Settings.Prop.EnablePlaytimeTracking)
            {
                EmptyMessage = Strings.Menu_Integrations_PlaytimeStats_TrackingOff;
                return;
            }

            var stats = new PlaytimeStats();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var summary = await stats.GetSummaryAsync(account.UserId, SelectedRange, now, TimeZoneInfo.Local, _loadCts.Token);
            if (seq != _loadSeq) return;

            Summary = summary;

            if (summary.SessionCount == 0)
            {
                EmptyMessage = Strings.Menu_Integrations_PlaytimeStats_NoData;
                return;
            }

            var totals = await stats.GetPerGameTotalsAsync(account.UserId, SelectedRange, now, TimeZoneInfo.Local, _loadCts.Token);
            if (seq != _loadSeq) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                GameTotals.Clear();
                foreach (var t in totals)
                {
                    GameTotals.Add(new GameTotalViewItem(
                        t.UniverseId,
                        t.TotalSeconds,
                        t.SessionCount,
                        t.LastPlayedAt,
                        FormatDuration(t.TotalSeconds),
                        FormatLastPlayed(t.LastPlayedAt)
                    ));
                }
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "Failed to load playtime stats");
            EmptyMessage = ex.Message;
        }
        finally
        {
            if (seq == _loadSeq)
            {
                IsLoading = false;
            }
        }
    }

    private void OnActiveAccountChanged(AccountManagerAccount? account)
    {
        OnPropertyChanged(nameof(SelectedAccount));
        OnPropertyChanged(nameof(ShowAccountSelector));
        _ = RefreshAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _loadCts.Cancel();
        _loadCts.Dispose();
        _accountManager.ActiveAccountChanged -= OnActiveAccountChanged;
    }

    public static string FormatDuration(long seconds)
    {
        if (seconds <= 0) return "—";
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{ts.Minutes}m";
    }

    public static string FormatLastPlayed(long unixSeconds)
    {
        if (unixSeconds <= 0) return "—";
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("g");
    }
}