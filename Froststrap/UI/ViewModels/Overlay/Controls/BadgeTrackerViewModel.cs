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
        private readonly FlashMessage _flash;
        private readonly Dictionary<long, DateTime> _removedAt = new();

        private Badge? _pending;

        private long _loadedUniverseId;
        private bool _checkingAwards;
        private bool _isRemoving;

        public event EventHandler<Badge>? BadgeEarned;

        public ObservableCollection<Badge> Badges { get; } = [];

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value)
                    return;

                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(CanRefresh));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }

        public bool CanRefresh => !IsBusy && !_isRemoving;

        public bool CanEdit => !_isRemoving;

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
                    ? String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Badges_Progress, EarnedCount, Badges.Count)
                    : String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Badges_ProgressUnknown, Badges.Count);
            }
        }

        public string HeaderText => _flash.Text ?? CompletionText;

        public bool IsConfirming => _pending is not null && !_isRemoving;

        public string ConfirmText => _pending is null
            ? String.Empty
            : String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Badges_ConfirmOne, _pending.Name);

        public bool IsRemoving => _isRemoving;

        public string RemovingText => _pending is null
            ? String.Empty
            : String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Badges_Removing, 1, 1);

        public bool ShowSummary => !IsConfirming && !_isRemoving;

        public bool ShowEmptyState => !IsBusy && !Badges.Any();

        public string EmptyText => InGame
            ? Strings.Menu_Overlay_Badges_Empty
            : Strings.Menu_Overlay_Badges_NotInGame;

        private bool InGame => _activityWatcher?.InGame == true && _activityWatcher.Data.UniverseId != 0;

        public ICommand RefreshCommand => new RelayCommand(async () => await LoadAsync(true));

        public ICommand RemoveCommand => new RelayCommand<Badge>(badge =>
        {
            if (badge?.CanRemove == true)
                Ask(badge);
        });

        public ICommand ConfirmRemoveCommand => new RelayCommand(async () => await RemovePendingAsync());

        public ICommand CancelRemoveCommand => new RelayCommand(() => Ask(null));

        public BadgeTrackerViewModel(ActivityWatcher? activityWatcher)
        {
            _activityWatcher = activityWatcher;

            _flash = new FlashMessage(TimeSpan.FromSeconds(4), () =>
            {
                OnPropertyChanged(nameof(HeaderText));
            });

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
            if (IsBusy || _isRemoving)
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

                foreach (Badge badge in badges.Where(x => x.Awarded && !StillEarned(x.Id, x.AwardedDate)))
                {
                    badge.Awarded = false;
                    badge.AwardedDate = null;
                }

                _pending = null;

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
            if (_checkingAwards || IsBusy || _isRemoving || !InGame)
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

                if (universeId != _loadedUniverseId || _isRemoving)
                    return;

                var earned = unearned.Where(x => awarded.TryGetValue(x.Id, out DateTime at) && StillEarned(x.Id, at)).ToList();

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

        private bool StillEarned(long badgeId, DateTime? awardedAt) =>
            !_removedAt.TryGetValue(badgeId, out DateTime removedAt) || (awardedAt is DateTime at && at.ToUniversalTime() > removedAt);

        private void Ask(Badge? badge)
        {
            if (_isRemoving)
                return;

            _pending = badge;

            RemovalChanged();
        }

        private async Task RemovePendingAsync()
        {
            if (_isRemoving || _pending is null)
                return;

            Badge target = _pending;

            _isRemoving = true;

            RemovalChanged();

            try
            {
                await BadgesApi.RemoveAsync(target.Id);

                _removedAt[target.Id] = DateTime.UtcNow;

                target.Awarded = false;
                target.AwardedDate = null;

                App.Logger.Info($"Removed badge {target.Id}");

                _isRemoving = false;
                _pending = null;

                Show(Badges.ToList());

                Refreshed();

                if (_activityWatcher is not null && Badges.Any(x => x.AwardedKnown && !x.Awarded))
                    _awardTimer.Start();

                _flash.Show(String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Badges_RemovedOne, target.Name));
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to remove badge {target.Id}");
                App.Logger.Error(ex);

                _isRemoving = false;
                _pending = null;

                Refreshed();

                _flash.Show(String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Badges_RemoveFailed, 1, 1));
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

            _pending = null;
            _loadedUniverseId = 0;

            Refreshed();
        }

        private void RemovalChanged()
        {
            OnPropertyChanged(nameof(IsConfirming));
            OnPropertyChanged(nameof(ConfirmText));
            OnPropertyChanged(nameof(IsRemoving));
            OnPropertyChanged(nameof(RemovingText));
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanRefresh));
            OnPropertyChanged(nameof(ShowSummary));
        }

        private void Refreshed()
        {
            OnPropertyChanged(nameof(EarnedCount));
            OnPropertyChanged(nameof(CompletionPercentage));
            OnPropertyChanged(nameof(CompletionText));
            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(EmptyText));

            RemovalChanged();
        }
    }
}