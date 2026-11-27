// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Data.Converters;
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
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;

using TimeRange = Froststrap.Services.TimeRange;
using ChartType = Froststrap.Services.ChartType;

namespace Froststrap.UI.ViewModels.Dialogs;

internal sealed partial class PlaytimeStatsDialogViewModel : NotifyPropertyChangedViewModel, IDisposable
{
    private readonly AccountManager _accountManager;
    private readonly Dictionary<long, string> _gameNames = new();
    private CancellationTokenSource _loadCts = new();
    private int _loadSeq;   // full reloads only
    private int _chartSeq;  // chart rebuilds only. Must stay separate from _loadSeq.
    private bool _disposed;

    /// <summary>Label for enum values in the combo boxes. Replaces EnumNameConverter, which showed "Unknown".</summary>
    public static readonly IValueConverter EnumLabelConverter = new FuncValueConverter<object?, string>(value => value switch
    {
        TimeRange.Last7Days => "Last 7 days",
        TimeRange.Last30Days => "Last 30 days",
        TimeRange.Last90Days => "Last 90 days",
        TimeRange.AllTime => "All time",
        ChartType.Column => "Column",
        ChartType.Bar => "Bar",
        ChartType.Pie => "Pie",
        ChartType.Histogram => "Session length",
        _ => value?.ToString() ?? string.Empty
    });

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
                OnPropertyChanged(nameof(IsPieSelected));
                OnPropertyChanged(nameof(IsCartesianSelected));
                _ = RefreshChartsAsync();
            }
        }
    }
    private ChartType _selectedChartType = ChartType.Column;

    public ObservableCollection<ChartType> ChartTypes { get; }

    public bool IsPieSelected => SelectedChartType == ChartType.Pie;
    public bool IsCartesianSelected => SelectedChartType != ChartType.Pie;

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
                // Derived props must raise too, otherwise the cards, charts and list never appear.
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
        string DisplayName,
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

    // Chart properties
    public ISeries[] MainChartSeries { get => _mainChartSeries; private set => SetProperty(ref _mainChartSeries, value); }
    private ISeries[] _mainChartSeries = [];

    public ISeries[] PieChartSeries { get => _pieChartSeries; private set => SetProperty(ref _pieChartSeries, value); }
    private ISeries[] _pieChartSeries = [];

    public Axis[] MainChartXAxes { get => _mainChartXAxes; private set => SetProperty(ref _mainChartXAxes, value); }
    private Axis[] _mainChartXAxes = [new Axis()];

    public Axis[] MainChartYAxes { get => _mainChartYAxes; private set => SetProperty(ref _mainChartYAxes, value); }
    private Axis[] _mainChartYAxes = [new Axis()];

    public ISeries[] TrendChartSeries { get => _trendChartSeries; private set => SetProperty(ref _trendChartSeries, value); }
    private ISeries[] _trendChartSeries = [];

    public Axis[] TrendChartXAxes { get => _trendChartXAxes; private set => SetProperty(ref _trendChartXAxes, value); }
    private Axis[] _trendChartXAxes = [new Axis()];

    public Axis[] TrendChartYAxes { get => _trendChartYAxes; private set => SetProperty(ref _trendChartYAxes, value); }
    private Axis[] _trendChartYAxes = [new Axis()];

    public ICommand RefreshCommand { get; }

    public async Task RefreshAsync()
    {
        // A new refresh always cancels the previous one. No "if (IsLoading) return" guard:
        // it dropped range changes and, combined with the old seq bug, locked the dialog forever.
        var seq = ++_loadSeq;
        var previous = _loadCts;
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;
        await previous.CancelAsync();

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

            var summary = await stats.GetSummaryAsync(account.UserId, SelectedRange, now, TimeZoneInfo.Local, ct);
            if (seq != _loadSeq) return;

            if (summary.SessionCount == 0)
            {
                // Summary stays null so HasSummary is false and the empty state shows.
                EmptyMessage = Strings.Menu_Integrations_PlaytimeStats_NoData;
                return;
            }

            var totals = await stats.GetPerGameTotalsAsync(account.UserId, SelectedRange, now, TimeZoneInfo.Local, ct);
            if (seq != _loadSeq) return;

            await LoadGameNamesAsync(totals.Select(t => t.UniverseId), ct);
            if (seq != _loadSeq) return;

            GameTotals.Clear();
            foreach (var t in totals)
            {
                GameTotals.Add(new GameTotalViewItem(
                    t.UniverseId,
                    GameName(t.UniverseId),
                    t.TotalSeconds,
                    t.SessionCount,
                    t.LastPlayedAt,
                    FormatDuration(t.TotalSeconds),
                    FormatLastPlayed(t.LastPlayedAt)
                ));
            }

            Summary = summary;
            await RefreshChartsAsync(ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            App.Logger.Error(ex, Strings.Menu_Integrations_PlaytimeStats_FailedToLoadStats);
            EmptyMessage = Strings.Menu_Integrations_PlaytimeStats_FailedToLoadStats;
        }
        finally
        {
            // Only the newest refresh clears the spinner.
            if (seq == _loadSeq)
                IsLoading = false;
        }
    }

    private async Task RefreshChartsAsync(CancellationToken outer = default)
    {
        var account = SelectedAccount;
        if (account == null || Summary == null) return;

        var seq = ++_chartSeq; // NOT _loadSeq
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            using var stats = new PlaytimeStats();

            var main = await BuildMainChartAsync(stats, account.UserId, now, outer);
            var trend = await BuildTrendChartAsync(stats, account.UserId, now, outer);
            if (seq != _chartSeq) return;

            // Pie goes to its own control. Cartesian series in a PieChart (and the reverse) draw nothing.
            MainChartSeries = main.IsPie ? [] : main.Series;
            PieChartSeries = main.IsPie ? main.Series : [];
            MainChartXAxes = main.XAxes;
            MainChartYAxes = main.YAxes;

            TrendChartSeries = trend.Series;
            TrendChartXAxes = trend.XAxes;
            TrendChartYAxes = trend.YAxes;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            App.Logger.Error(ex, Strings.Menu_Integrations_PlaytimeStats_FailedToLoadCharts);
        }
    }

    private sealed record ChartBundle(ISeries[] Series, Axis[] XAxes, Axis[] YAxes, bool IsPie = false);

    private async Task<ChartBundle> BuildMainChartAsync(PlaytimeStats stats, long userId, long now, CancellationToken ct)
    {
        if (SelectedChartType == ChartType.Histogram)
        {
            var bins = await stats.GetSessionLengthHistogramAsync(userId, SelectedRange, now, TimeZoneInfo.Local, ct);
            var binLabels = bins.Select(b => b.Label).ToArray();

            return new ChartBundle(
                [
                    new ColumnSeries<double>
                    {
                        Values = bins.Select(b => (double)b.Count).ToArray(),
                        Name = Strings.Menu_Integrations_PlaytimeStats_Sessions,
                        Fill = new SolidColorPaint(GetAccentColor()),
                        Stroke = null
                    }
                ],
                [CategoryAxis(binLabels)],
                [CountAxis()]);
        }

        var totals = await stats.GetPerGameTotalsAsync(userId, SelectedRange, now, TimeZoneInfo.Local, ct);
        var top = PlaytimeStats.TopNWithOther(totals, 8);
        var names = top.Select(t => ChartName(t.UniverseId)).ToArray();
        var hours = top.Select(t => t.TotalSeconds / 3600.0).ToArray();

        switch (SelectedChartType)
        {
            case ChartType.Pie:
                var palette = GetPiePalette();
                var slices = top
                    .Select((t, i) => BuildPieSlice(names[i], t.TotalSeconds, palette[i % palette.Length]))
                    .ToArray();
                return new ChartBundle(slices, [new Axis()], [new Axis()], IsPie: true);

            case ChartType.Bar:
                // Horizontal bars: categories on Y, hours on X.
                return new ChartBundle(
                    [
                        new RowSeries<double>
                        {
                            Values = hours,
                            Name = Strings.Menu_Integrations_PlaytimeStats_Hours,
                            Fill = new SolidColorPaint(GetAccentColor()),
                            Stroke = null,
                            // Row series: category axis is Y, so set both labels explicitly.
                            YToolTipLabelFormatter = p => FormatDuration((long)(p.Coordinate.PrimaryValue * 3600)),
                            XToolTipLabelFormatter = p => names[p.Index]
                        }
                    ],
                    [HoursAxis()],
                    [CategoryAxis(names)]);

            default: // Column
                return new ChartBundle(
                    [
                        new ColumnSeries<double>
                        {
                            Values = hours,
                            Name = Strings.Menu_Integrations_PlaytimeStats_Hours,
                            Fill = new SolidColorPaint(GetAccentColor()),
                            Stroke = null,
                            // Game name comes from the X axis labels automatically.
                            YToolTipLabelFormatter = p => FormatDuration((long)(p.Coordinate.PrimaryValue * 3600))
                        }
                    ],
                    [CategoryAxis(names)],
                    [HoursAxis()]);
        }
    }

    private static PieSeries<double> BuildPieSlice(string name, long seconds, SKColor color)
    {
        // One series per slice: gives each its own color and legend entry.
        return new PieSeries<double>
        {
            Values = new[] { (double)seconds },
            Name = name,
            Fill = new SolidColorPaint(color),
            InnerRadius = 50,
            DataLabelsSize = 12,
            DataLabelsPaint = new SolidColorPaint(SKColors.White),
            DataLabelsFormatter = point => FormatDuration((long)point.Coordinate.PrimaryValue),
            ToolTipLabelFormatter = point => $"{name}: {FormatDuration((long)point.Coordinate.PrimaryValue)}"
        };
    }

    private async Task<ChartBundle> BuildTrendChartAsync(PlaytimeStats stats, long userId, long now, CancellationToken ct)
    {
        var buckets = await stats.GetTrendAsync(userId, SelectedRange, now, TimeZoneInfo.Local, ct);

        var labels = buckets
            .Select(b => TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(b.BucketStartUtc, DateTimeKind.Utc), TimeZoneInfo.Local)
                .ToString("MMM d", CultureInfo.CurrentCulture))
            .ToArray();
        var hours = buckets.Select(b => b.Seconds / 3600.0).ToArray();

        return new ChartBundle(
            [
                new LineSeries<double>
                {
                    Values = hours,
                    Name = Strings.Menu_Integrations_PlaytimeStats_Hours,
                    Fill = null,
                    Stroke = new SolidColorPaint(GetAccentColor(), 3),
                    GeometrySize = 6,
                    GeometryFill = new SolidColorPaint(GetAccentColor()),
                    GeometryStroke = new SolidColorPaint(SKColors.White, 2),
                    LineSmoothness = 0.3,
                    // Date comes from the X axis labels automatically.
                    YToolTipLabelFormatter = p => FormatDuration((long)(p.Coordinate.PrimaryValue * 3600))
                }
            ],
            [
                new Axis
                {
                    Labels = labels,
                    LabelsRotation = -45,
                    SeparatorsPaint = new SolidColorPaint(SKColors.Transparent),
                    TicksPaint = new SolidColorPaint(SKColors.Gray),
                    TextSize = 10
                }
            ],
            [HoursAxis()]);
    }

    private static Axis CategoryAxis(string[] labels) => new()
    {
        Labels = labels,
        LabelsRotation = 0,
        SeparatorsPaint = new SolidColorPaint(SKColors.Transparent),
        TicksPaint = new SolidColorPaint(SKColors.Gray),
        TextSize = 11
    };

    private static Axis HoursAxis() => new()
    {
        Labeler = value => $"{value:0.##}h",
        SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(50)),
        TicksPaint = new SolidColorPaint(SKColors.Gray),
        TextSize = 11,
        MinLimit = 0
    };

    private static Axis CountAxis() => new()
    {
        Labeler = value => $"{value:0}",
        MinStep = 1,
        SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(50)),
        TicksPaint = new SolidColorPaint(SKColors.Gray),
        TextSize = 11,
        MinLimit = 0
    };

    private static SKColor GetAccentColor() => new(0x00, 0x78, 0xD4);

    private static SKColor[] GetPiePalette() =>
    [
        new SKColor(0x00, 0x78, 0xD4),
        new SKColor(0x10, 0x7C, 0x10),
        new SKColor(0xD8, 0x3B, 0x01),
        new SKColor(0xFF, 0xB9, 0x00),
        new SKColor(0x5C, 0x2D, 0x91),
        new SKColor(0x00, 0x82, 0x72),
        new SKColor(0xD8, 0x3B, 0x7E),
        new SKColor(0x6B, 0x69, 0xD6),
        new SKColor(0x8E, 0x8E, 0x8E)
    ];

    private async Task LoadGameNamesAsync(IEnumerable<long> universeIds, CancellationToken ct)
    {
        try
        {
            using var db = new PlaytimeDb();
            foreach (var id in universeIds)
            {
                ct.ThrowIfCancellationRequested();
                if (id <= 0 || _gameNames.ContainsKey(id)) continue;

                var game = await db.GetGameAsync(id, ct);
                if (!string.IsNullOrWhiteSpace(game?.Name))
                    _gameNames[id] = game.Name!;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Names are cosmetic. Fall back to "Universe {id}".
            App.Logger.Error(ex, "Failed to load playtime game names");
        }
    }

    private string GameName(long universeId)
    {
        if (universeId == 0)
            return Strings.Menu_Integrations_PlaytimeStats_Other;

        return _gameNames.TryGetValue(universeId, out var name)
            ? name
            : string.Format(CultureInfo.CurrentCulture, Strings.Menu_Integrations_PlaytimeStats_UniverseFormat, universeId);
    }

    private static readonly SKTypeface ChartTypeface = SKTypeface.Default;

    /// <summary>
    /// Name for chart labels/tooltips. Skia charts have no per-glyph font fallback: one unsupported
    /// symbol (Roblox names love them) turns the whole label into boxes. Strip what the font can't draw.
    /// The game list keeps the full name, since Avalonia does its own fallback.
    /// </summary>
    private string ChartName(long universeId)
    {
        if (universeId == 0)
            return Strings.Menu_Integrations_PlaytimeStats_Other;

        if (_gameNames.TryGetValue(universeId, out var name))
        {
            var safe = ChartSafe(name);
            if (safe.Length > 0)
                return safe;
        }

        return string.Format(CultureInfo.CurrentCulture, Strings.Menu_Integrations_PlaytimeStats_UniverseFormat, universeId);
    }

    private static string ChartSafe(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var rune in text.Replace('\r', ' ').Replace('\n', ' ').EnumerateRunes())
        {
            if (ChartTypeface.ContainsGlyph(rune.Value))
                sb.Append(rune.ToString());
        }

        var result = EmptyBracketsRegex().Replace(sb.ToString(), ""); // brackets left empty
        return WhitespaceRegex().Replace(result, " ").Trim();
    }

    [GeneratedRegex(@"[\[\(\{]\s*[\]\)\}]")]
    private static partial Regex EmptyBracketsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

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
