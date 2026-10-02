using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.Input;

using Froststrap.Integrations;

using BadgesApi = Froststrap.RobloxInterfaces.Badges;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    internal class BadgeTrackerViewModel : NotifyPropertyChangedViewModel
    {
        private static readonly TimeSpan AwardCheckInterval = TimeSpan.FromSeconds(60);

        private readonly ActivityWatcher? _activityWatcher;
        private readonly DispatcherTimer _awardTimer = new() { Interval = AwardCheckInterval };

        private long _loadedUniverseId;
        private bool _checkingAwards;

        public event EventHandler<Badge>? BadgeEarned;

        public ObservableCollection<Badge> Badges { get; } = [];

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(CanRefresh));
            }
        }

        public bool CanRefresh => !IsBusy;

        public int EarnedCount => Badges.Count(x => x.Awarded);

        private bool ProgressKnown => Badges.Any(x => x.AwardedKnown);

        public double CompletionPercentage => Badges.Count > 0 && ProgressKnown
            ? (double)EarnedCount / Badges.Count * 100
            : 0;

        public string CompletionText
        {
            get
            {
                if (Badges.Count == 0)
                    return String.Empty;

                return ProgressKnown
                    ? String.Format(CultureInfo.CurrentCulture, Strings.Menu_Overlay_Badges_Progress, EarnedCount, Badges.Count)
                    : String.Format(CultureInfo.CurrentCulture, Strings.Menu_Overlay_Badges_ProgressUnknown, Badges.Count);
            }
        }

        public bool HasBadges => Badges.Count > 0;
        public bool ShowEmptyState => !IsBusy && Badges.Count == 0;

        public string EmptyText => InGame
            ? Strings.Menu_Overlay_Badges_Empty
            : Strings.Menu_Overlay_Badges_NotInGame;

        private bool InGame => _activityWatcher?.InGame == true && _activityWatcher.Data.UniverseId != 0;

        public ICommand RefreshCommand => new RelayCommand(async () => await LoadAsync(true));

        public BadgeTrackerViewModel(ActivityWatcher? activityWatcher)
        {
            _activityWatcher = activityWatcher;

            App.Cookies.WatchAccount(this, static vm => vm.OnAccountChanged());

            if (_activityWatcher is null)
                return;

            _awardTimer.Tick += async (_, _) => await CheckForAwardsAsync();

            _activityWatcher.OnGameJoin += async (_, _) =>
            {
                await Dispatcher.UIThread.InvokeAsync(async () => await LoadAsync());
            };

            _activityWatcher.OnGameLeave += (_, _) =>
            {
                Dispatcher.UIThread.Post(Clear);
            };
        }

        private void OnAccountChanged() =>
            Dispatcher.UIThread.Post(() => _ = LoadAsync(true));

        public async Task LoadAsync(bool force = false)
        {
            if (IsBusy)
                return;

            if (!InGame)
            {
                Clear();
                return;
            }

            long universeId = _activityWatcher!.Data.UniverseId;

            if (!force && universeId == _loadedUniverseId && Badges.Count > 0)
                return;

            IsBusy = true;

            try
            {
                var badges = await BadgesApi.FetchAsync(universeId, _activityWatcher.Data.UserId);

                Show(badges);

                _loadedUniverseId = universeId;

                if (Badges.Any(x => x.AwardedKnown && !x.Awarded))
                    _awardTimer.Start();
                else
                    _awardTimer.Stop();
            }
            catch (Exception ex)
            {
                App.Logger.Warn("Failed to load badges");
                App.Logger.Error(ex);
            }
            finally
            {
                IsBusy = false;
                Refreshed();
            }
        }

        public async Task CheckForAwardsAsync()
        {
            if (_checkingAwards || IsBusy || !InGame)
                return;

            var unearned = Badges.Where(x => x.AwardedKnown && !x.Awarded).ToList();

            if (unearned.Count == 0)
            {
                _awardTimer.Stop();
                return;
            }

            long universeId = _loadedUniverseId;

            _checkingAwards = true;

            try
            {
                var awarded = await BadgesApi.AwardedDatesAsync(
                    _activityWatcher!.Data.UserId,
                    unearned.Select(x => x.Id));

                if (universeId != _loadedUniverseId)
                    return;

                var earned = unearned.Where(x => awarded.ContainsKey(x.Id)).ToList();

                if (earned.Count == 0)
                    return;

                foreach (Badge badge in earned)
                {
                    badge.Awarded = true;
                    badge.AwardedDate = awarded[badge.Id];
                }

                App.Logger.Info($"Earned {earned.Count} badge(s) since the last check");

                Show(Badges.ToList());
                Refreshed();

                foreach (Badge badge in earned)
                    BadgeEarned?.Invoke(this, badge);
            }
            catch (Exception ex)
            {
                App.Logger.Warn("Failed to check for new badges");
                App.Logger.Error(ex);
            }
            finally
            {
                _checkingAwards = false;
            }
        }

        private void Show(IEnumerable<Badge> badges)
        {
            var ordered = badges
                .OrderBy(x => x.Awarded)
                .ThenByDescending(x => x.WinRatePercentage)
                .ToList();

            Badges.Clear();

            foreach (Badge badge in ordered)
            {
                Badges.Add(badge);
                _ = badge.LoadIconAsync();
            }
        }

        private void Clear()
        {
            _awardTimer.Stop();

            Badges.Clear();
            _loadedUniverseId = 0;

            Refreshed();
        }

        private void Refreshed()
        {
            OnPropertyChanged(nameof(EarnedCount));
            OnPropertyChanged(nameof(CompletionPercentage));
            OnPropertyChanged(nameof(CompletionText));
            OnPropertyChanged(nameof(HasBadges));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(EmptyText));
        }
    }
}