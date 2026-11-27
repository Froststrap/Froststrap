// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations.AccountManager;
using Froststrap.Localisation;
using Froststrap.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

using TimeRange = Froststrap.Services.TimeRange;
using ChartType = Froststrap.Services.ChartType;

namespace Froststrap.UI.ViewModels.Dialogs;

internal sealed class PlaytimeStatsDialogViewModel : NotifyPropertyChangedViewModel, IDisposable
{
    private readonly AccountManager _accountManager;
    private CancellationTokenSource _loadCts = new();
    private int _loadSeq;
    private int _chartSeq;
    private bool _disposed;

    public PlaytimeStatsDialogViewModel()
    {
        _accountManager = AccountManager.Shared;
        _accountManager.ActiveAccountChanged += OnActiveAccountChanged;

        Accounts = new ObservableCollection<AccountManagerAccount>(_accountManager.Accounts);
        Ranges = new ObservableCollection<TimeRange>(Enum.GetValues<TimeRange>());
        ChartTypes = new ObservableCollection<ChartType>(Enum.GetValues<ChartType>());

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

    public TimeRange SelectedRange
    {
        get => _selectedRange;
        set
        {
            if (SetProperty(ref _selectedRange, value))
                _ = RefreshAsync();
        }
    }
    private TimeRange _selectedRange = TimeRange.Last30Days;

    public ObservableCollection<TimeRange> Ranges { get; }

    public ChartType SelectedChartType
    {
        get => _selectedChartType;
        set
        {
            if (SetProperty(ref _selectedChartType, value))
            {
                OnPropertyChanged(nameof(IsPieChart));
                OnPropertyChanged(nameof(IsNotPieChart));
                _ = RefreshChartsAsync(CancellationToken.None);
            }
        }
    }
    private ChartType _selectedChartType = ChartType.Column;

    public bool IsPieChart => SelectedChartType == ChartType.Pie;
    public bool IsNotPieChart => SelectedChartType != ChartType.Pie;

    public ObservableCollection<ChartType> ChartTypes { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }
    private bool _isLoading;

    public SummaryResult? Summary
    {
        get => _summary;
        private set
        {
            if (SetProperty(ref _summary, value))
            {
                OnPropertyChanged(nameof(HasSummary));
                OnPropertyChanged(nameof(SummaryTotalTime));
                OnPropertyChanged(nameof(SummaryGamesPlayed));
                OnPropertyChanged(nameof(SummarySessions));
                OnPropertyChanged(nameof(SummaryAvgSession));
                OnPropertyChanged(nameof(SummaryLongestSession));
            }
        }
    }
    private SummaryResult? _summary;

    public ObservableCollection<GameTotalViewItem> GameTotals { get; } = [];

    internal sealed record GameTotalViewItem(
        long UniverseId,
        long TotalSeconds,
        int SessionCount,
        long LastPlayedAt,
        string FormattedTime,
        string FormattedLastPlayed,
        string GameName
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

    // Chart properties
    public ISeries[]? MainChartSeries { get; private set; }
    public Axis[]? MainChartXAxes { get; private set; }
    public Axis[]? MainChartYAxes { get; private set; }
    public ISeries[]? TrendChartSeries { get; private set; }
    public Axis[]? TrendChartXAxes { get; private set; }
    public Axis[]? TrendChartYAxes { get; private set; }

    public ICommand RefreshCommand { get; }

    public async Task RefreshAsync()
    {
        var seq = ++_loadSeq;
        await _loadCts.CancelAsync();
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

            using var stats = new PlaytimeStats();
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

            var gameNames = await GetGameNamesAsync(totals.Select(t => t.UniverseId).ToList());

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                GameTotals.Clear();
                foreach (var t in totals)
                {
                    gameNames.TryGetValue(t.UniverseId, out var name);
                    GameTotals.Add(new GameTotalViewItem(
                        t.UniverseId,
                        t.TotalSeconds,
                        t.SessionCount,
                        t.LastPlayedAt,
                        FormatDuration(t.TotalSeconds),
                        FormatLastPlayed(t.LastPlayedAt),
                        name ?? (t.UniverseId == 0 ? Strings.Menu_Integrations_PlaytimeStats_Other : $"{Strings.Menu_Integrations_PlaytimeStats_Game} {t.UniverseId}")
                    ));
                }
            });

            Summary = summary;
            await RefreshChartsAsync(_loadCts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            App.Logger.Error(ex, Strings.Menu_Integrations_PlaytimeStats_FailedToLoadStats);
            EmptyMessage = Strings.Menu_Integrations_PlaytimeStats_FailedToLoadStats;
        }
        finally
        {
            if (seq == _loadSeq)
            {
                IsLoading = false;
            }
        }
    }

    private static async Task<Dictionary<long, string>> GetGameNamesAsync(List<long> universeIds)
    {
        var result = new Dictionary<long, string>();
        if (universeIds.Count == 0) return result;

        try
        {
            using var db = new PlaytimeDb();
            foreach (var id in universeIds)
            {
                if (id == 0) continue; // "Other" aggregate
                var game = await db.GetGameAsync(id);
                if (game?.Name != null)
                    result[id] = game.Name;
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "Failed to load game names");
        }
        return result;
    }

    private async Task RefreshChartsAsync(CancellationToken ct)
    {
        var account = SelectedAccount;
        if (account == null || Summary == null) return;

        var seq = ++_chartSeq;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            using var stats = new PlaytimeStats();

            // Build main chart
            var (mainSeries, mainLabels) = await BuildMainChartSeriesAsync(stats, account.UserId, now, ct);
            if (seq != _chartSeq) return;

            MainChartSeries = mainSeries;
            MainChartXAxes = BuildMainChartXAxes(mainLabels);
            MainChartYAxes = BuildMainChartYAxes();

            // Build trend chart
            var (trendSeries, trendLabels) = await BuildTrendChartSeriesAsync(stats, account.UserId, now, ct);
            if (seq != _chartSeq) return;

            TrendChartSeries = trendSeries;
            TrendChartXAxes = BuildTrendChartXAxes(trendLabels);
            TrendChartYAxes = BuildTrendChartYAxes();

            OnPropertyChanged(nameof(MainChartSeries));
            OnPropertyChanged(nameof(MainChartXAxes));
            OnPropertyChanged(nameof(MainChartYAxes));
            OnPropertyChanged(nameof(TrendChartSeries));
            OnPropertyChanged(nameof(TrendChartXAxes));
            OnPropertyChanged(nameof(TrendChartYAxes));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            App.Logger.Error(ex, Strings.Menu_Integrations_PlaytimeStats_FailedToLoadCharts);
        }
    }

    private async Task<(ISeries[] Series, string[] Labels)> BuildMainChartSeriesAsync(PlaytimeStats stats, long userId, long now, CancellationToken ct)
    {
        var totals = await stats.GetPerGameTotalsAsync(userId, SelectedRange, now, TimeZoneInfo.Local, ct);
        var top8 = PlaytimeStats.TopNWithOther(totals, 8);

        var gameNames = await GetGameNamesAsync(top8.Select(t => t.UniverseId).ToList());
        var labels = top8.Select(t => t.UniverseId == 0
            ? Strings.Menu_Integrations_PlaytimeStats_Other
            : (gameNames.TryGetValue(t.UniverseId, out var n) ? n : $"{Strings.Menu_Integrations_PlaytimeStats_Game} {t.UniverseId}"))
            .ToArray();

        var series = SelectedChartType switch
        {
            ChartType.Column => BuildColumnSeries(top8, labels),
            ChartType.Bar => BuildBarSeries(top8, labels),
            ChartType.Pie => BuildPieSeries(top8, labels),
            ChartType.Histogram => BuildHistogramSeries(await stats.GetSessionLengthHistogramAsync(userId, SelectedRange, now, TimeZoneInfo.Local, ct)),
            _ => BuildColumnSeries(top8, labels)
        };

        return (series, labels);
    }

    private static ISeries[] BuildColumnSeries(List<GameTotalResult> totals, string[] labels)
    {
        var values = totals.Select(t => (double)t.TotalSeconds / 3600.0).ToArray();

        return new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = values,
                Name = Strings.Menu_Integrations_PlaytimeStats_Hours,
                Fill = new SolidColorPaint(GetAccentColor()),
                Stroke = null
            }
        };
    }

    private static ISeries[] BuildBarSeries(List<GameTotalResult> totals, string[] labels)
    {
        var values = totals.Select(t => (double)t.TotalSeconds / 3600.0).ToArray();

        return new ISeries[]
        {
            new RowSeries<double>
            {
                Values = values,
                Name = Strings.Menu_Integrations_PlaytimeStats_Hours,
                Fill = new SolidColorPaint(GetAccentColor()),
                Stroke = null
            }
        };
    }

    private static ISeries[] BuildPieSeries(List<GameTotalResult> totals, string[] labels)
    {
        var values = totals.Select(t => (double)t.TotalSeconds).ToArray();

        var pieSeries = new PieSeries<double>
        {
            Values = values,
            InnerRadius = 50,
            DataLabelsSize = 12,
            DataLabelsPaint = new SolidColorPaint(SKColors.White),
            DataLabelsFormatter = point => labels[point.Index],
            ToolTipLabelFormatter = point => $"{labels[point.Index]}: {FormatDuration((long)point.Coordinate.PrimaryValue)}"
        };

        return new ISeries[] { pieSeries };
    }

    private static ISeries[] BuildHistogramSeries(List<HistogramBinResult> bins)
    {
        var values = bins.Select(b => (double)b.Count).ToArray();
        var labels = bins.Select(b => b.Label).ToArray();

        return new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = values,
                Name = Strings.Menu_Integrations_PlaytimeStats_Sessions,
                Fill = new SolidColorPaint(GetAccentColor()),
                Stroke = null
            }
        };
    }

    private static Axis[] BuildMainChartXAxes(string[] labels)
    {
        return new Axis[]
        {
            new Axis
            {
                Labels = labels,
                LabelsRotation = 0,
                SeparatorsPaint = new SolidColorPaint(SKColors.Transparent),
                TicksPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 11
            }
        };
    }

    private static Axis[] BuildMainChartYAxes()
    {
        return new Axis[]
        {
            new Axis
            {
                Labeler = value => $"{value}h",
                SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(50)),
                TicksPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 11,
                MinLimit = 0
            }
        };
    }

    private async Task<(ISeries[] Series, string[] Labels)> BuildTrendChartSeriesAsync(PlaytimeStats stats, long userId, long now, CancellationToken ct)
    {
        var buckets = await stats.GetTrendAsync(userId, SelectedRange, now, TimeZoneInfo.Local, ct);

        var labels = buckets.Select(b => TimeZoneInfo.ConvertTimeFromUtc(new DateTimeOffset(b.BucketStartUtc).DateTime, TimeZoneInfo.Local).ToString(
            SelectedRange == TimeRange.Last90Days || SelectedRange == TimeRange.AllTime
                ? "MMM d"
                : "MMM d", CultureInfo.InvariantCulture)).ToArray();

        var series = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = buckets.Select(b => (double)b.Seconds / 3600.0).ToArray(),
                Name = Strings.Menu_Integrations_PlaytimeStats_Hours,
                Fill = null,
                Stroke = new SolidColorPaint(GetAccentColor(), 3),
                GeometrySize = 6,
                GeometryFill = new SolidColorPaint(GetAccentColor()),
                GeometryStroke = new SolidColorPaint(SKColors.White, 2),
                LineSmoothness = 0.3
            }
        };

        return (series, labels);
    }

    private static Axis[] BuildTrendChartXAxes(string[]? labels = null)
    {
        return new Axis[]
        {
            new Axis
            {
                LabelsRotation = -45,
                SeparatorsPaint = new SolidColorPaint(SKColors.Transparent),
                TicksPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 10,
                Labels = labels ?? Array.Empty<string>()
            }
        };
    }

    private static Axis[] BuildTrendChartYAxes()
    {
        return new Axis[]
        {
            new Axis
            {
                Labeler = value => $"{value}h",
                SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(50)),
                TicksPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 11,
                MinLimit = 0
            }
        };
    }

    private static SKColor GetAccentColor()
    {
        return new SKColor(0x00, 0x78, 0xD4);
    }

    private static SolidColorPaint[] GetPieColors(int count)
    {
        var palette = new[]
        {
            new SKColor(0x00, 0x78, 0xD4),
            new SKColor(0x10, 0x7C, 0x10),
            new SKColor(0xD8, 0x3B, 0x01),
            new SKColor(0xFF, 0xB9, 0x00),
            new SKColor(0x5C, 0x2D, 0x91),
            new SKColor(0x00, 0x82, 0x72),
            new SKColor(0xD8, 0x3B, 0x7E),
            new SKColor(0x6B, 0x69, 0xD6),
            new SKColor(0x8E, 0x8E, 0x8E)
        };

        return palette.Take(count).Select(c => new SolidColorPaint(c)).ToArray();
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
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }
}