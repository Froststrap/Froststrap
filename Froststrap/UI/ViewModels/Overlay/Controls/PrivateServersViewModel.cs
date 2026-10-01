using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.Input;

using Froststrap.Integrations;
using Froststrap.Models.APIs.Roblox;
using Froststrap.Models.Overlay;
using Froststrap.RobloxInterfaces;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    internal class PrivateServersViewModel : NotifyPropertyChangedViewModel
    {
        private readonly ActivityWatcher? _activityWatcher;
        private readonly Dictionary<long, PrivateServerDetails> _ownDetails = new();

        private DispatcherTimer? _statusTimer;
        private string? _status;
        private bool _visible;
        private long _loadedPlaceId;
        private bool _failed;
        private int _generation;
        private string? _cursor;
        private long? _managingId;
        private PrivateServerDetails? _details;

        public ObservableCollection<PrivateServerItem> Servers { get; } = [];
        public ObservableCollection<PrivateServerUser> AllowedUsers { get; } = [];

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                Refreshed();
            }
        }

        private bool _isLoadingMore;
        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            private set
            {
                _isLoadingMore = value;
                OnPropertyChanged(nameof(IsLoadingMore));
                OnPropertyChanged(nameof(HasMore));
            }
        }

        public bool HasMore => !String.IsNullOrEmpty(_cursor) && !_isLoadingMore && !IsManaging;

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                _isSaving = value;
                OnPropertyChanged(nameof(IsSaving));
                OnPropertyChanged(nameof(CanEdit));
            }
        }

        public bool CanEdit => _details is not null && !_isSaving;
        public bool IsManaging => _managingId is not null;

        public bool ListVisible => !IsManaging;
        public bool ManageVisible => IsManaging;

        public bool ShowEmptyState => !IsBusy && !IsManaging && Servers.Count == 0;

        public string EmptyText
        {
            get
            {
                if (!InGame)
                    return Strings.Menu_Overlay_Servers_NotInGame;

                if (!SignedIn)
                    return Strings.Menu_Overlay_PrivateServers_NeedsCookies;

                return _failed
                    ? Strings.Menu_Overlay_PrivateServers_LoadFailed
                    : Strings.Menu_Overlay_PrivateServers_None;
            }
        }

        public string StatusText => _status ?? String.Empty;
        public bool HasStatus => _status is not null;

        private string _serverName = String.Empty;
        public string ServerName
        {
            get => _serverName;
            private set
            {
                _serverName = value;
                OnPropertyChanged(nameof(ServerName));
            }
        }

        private string _gameName = String.Empty;
        public string GameName
        {
            get => _gameName;
            private set
            {
                _gameName = value;
                OnPropertyChanged(nameof(GameName));
            }
        }

        private string? _gameIcon;
        public string? GameIcon
        {
            get => _gameIcon;
            private set
            {
                _gameIcon = value;
                OnPropertyChanged(nameof(GameIcon));
            }
        }

        private string _editName = String.Empty;
        public string EditName
        {
            get => _editName;
            set
            {
                _editName = value;
                OnPropertyChanged(nameof(EditName));
            }
        }

        private bool _isEditingName;
        public bool IsEditingName
        {
            get => _isEditingName;
            private set
            {
                _isEditingName = value;
                OnPropertyChanged(nameof(IsEditingName));
                OnPropertyChanged(nameof(IsShowingName));
            }
        }

        public bool IsShowingName => !_isEditingName;

        private bool _isAddingPeople;
        public bool IsAddingPeople
        {
            get => _isAddingPeople;
            private set
            {
                _isAddingPeople = value;
                OnPropertyChanged(nameof(IsAddingPeople));
            }
        }

        public bool IsActive
        {
            get => _details?.Active == true;
            set
            {
                if (_details is null || _details.Active == value || _isSaving)
                    return;

                _ = ChangeAsync(
                    d => PrivateServers.SetActiveAsync(d, value),
                    value ? Strings.Menu_Overlay_PrivateServers_TurnedOn
                          : Strings.Menu_Overlay_PrivateServers_TurnedOff);
            }
        }

        public bool FriendsAllowed
        {
            get => _details?.Permissions?.FriendsAllowed == true;
            set
            {
                if (_details is null || FriendsAllowed == value || _isSaving)
                    return;

                _ = ChangeAsync(
                    d => PrivateServers.SetFriendsAllowedAsync(d, value),
                    Strings.Menu_Overlay_PrivateServers_Saved);
            }
        }

        public string Link => _details?.Link ?? String.Empty;
        public bool HasLink => !String.IsNullOrEmpty(_details?.Link);

        public string PriceText => _details?.Subscription?.Price is long price and > 0
            ? String.Format(Locale.CurrentCulture,
                Strings.Menu_Overlay_PrivateServers_PriceRobux,
                price.ToString("N0", Locale.CurrentCulture))
            : Strings.Menu_Overlay_PrivateServers_Free;

        public string RenewalText
        {
            get
            {
                PrivateServerSubscription? s = _details?.Subscription;
                if (s is null) return String.Empty;
                if (s.Expired) return Strings.Menu_Overlay_PrivateServers_Expired;
                if (s.Price is not > 0 || s.ExpirationDate is not DateTime date) return String.Empty;

                string when = date.ToLocalTime().ToString("d", Locale.CurrentCulture);

                return String.Format(Locale.CurrentCulture,
                    s.Active ? Strings.Menu_Overlay_PrivateServers_Renews : Strings.Menu_Overlay_PrivateServers_Ends,
                    when);
            }
        }

        public bool HasRenewal => !String.IsNullOrEmpty(RenewalText);
        public bool HasNoAllowedUsers => AllowedUsers.Count == 0;

        private string _addUsername = String.Empty;
        public string AddUsername
        {
            get => _addUsername;
            set
            {
                _addUsername = value;
                OnPropertyChanged(nameof(AddUsername));
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand LoadMoreCommand { get; }
        public ICommand JoinCommand { get; }
        public ICommand ConfigureCommand { get; }
        public ICommand ToggleRowActiveCommand { get; }
        public ICommand GenerateRowLinkCommand { get; }
        public ICommand CopyRowLinkCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand EditNameCommand { get; }
        public ICommand SaveNameCommand { get; }
        public ICommand CancelNameCommand { get; }
        public ICommand AddPeopleCommand { get; }
        public ICommand AddUserCommand { get; }
        public ICommand RemoveUserCommand { get; }
        public ICommand RegenerateLinkCommand { get; }
        public ICommand CopyLinkCommand { get; }

        private bool InGame => _activityWatcher?.InGame == true && _activityWatcher.Data.PlaceId != 0;
        private long PlaceId => _activityWatcher?.Data.PlaceId ?? 0;
        private static bool SignedIn => App.Settings.Prop.AllowCookieAccess && App.Cookies.Loaded;

        public PrivateServersViewModel(ActivityWatcher? activityWatcher)
        {
            _activityWatcher = activityWatcher;

            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync);
            JoinCommand = new RelayCommand<PrivateServerItem>(Join);
            ConfigureCommand = new AsyncRelayCommand<PrivateServerItem>(ConfigureAsync);

            ToggleRowActiveCommand = new AsyncRelayCommand<PrivateServerItem>(item =>
                RowChangeAsync(item,
                    d => PrivateServers.SetActiveAsync(d, !d.Active),
                    d => d.Active
                        ? Strings.Menu_Overlay_PrivateServers_TurnedOff
                        : Strings.Menu_Overlay_PrivateServers_TurnedOn));

            GenerateRowLinkCommand = new AsyncRelayCommand<PrivateServerItem>(item =>
                RowChangeAsync(item,
                    PrivateServers.RegenerateLinkAsync,
                    _ => Strings.Menu_Overlay_PrivateServers_NewLinkMade));

            CopyRowLinkCommand = new RelayCommand<PrivateServerItem>(item => CopyToClipboard(item?.Link));
            BackCommand = new RelayCommand(Back);

            EditNameCommand = new RelayCommand(() =>
            {
                EditName = _details?.Name ?? ServerName;
                IsEditingName = true;
            });

            SaveNameCommand = new AsyncRelayCommand(SaveNameAsync);
            CancelNameCommand = new RelayCommand(() => IsEditingName = false);

            AddPeopleCommand = new RelayCommand(() =>
            {
                AddUsername = String.Empty;
                IsAddingPeople = !IsAddingPeople;
            });

            AddUserCommand = new AsyncRelayCommand(AddUserAsync);
            RemoveUserCommand = new AsyncRelayCommand<PrivateServerUser>(RemoveUserAsync);

            RegenerateLinkCommand = new AsyncRelayCommand(() =>
                ChangeAsync(PrivateServers.RegenerateLinkAsync,
                    Strings.Menu_Overlay_PrivateServers_NewLinkMade));

            CopyLinkCommand = new RelayCommand(() => CopyToClipboard(Link));

            if (_activityWatcher is null)
                return;

            _activityWatcher.OnGameJoin += (_, _) => Dispatcher.UIThread.Post(OnGameJoin);
            _activityWatcher.OnGameLeave += (_, _) => Dispatcher.UIThread.Post(OnGameLeave);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;

            if (visible && !IsManaging && InGame && _loadedPlaceId != PlaceId)
                _ = LoadAsync();
        }

        private void OnGameJoin()
        {
            _loadedPlaceId = 0;
            StopManaging();

            if (_visible)
                _ = LoadAsync();
            else
                Refreshed();
        }

        private void OnGameLeave()
        {
            _generation++;
            _loadedPlaceId = 0;
            _cursor = null;

            StopManaging();

            Servers.Clear();
            _ownDetails.Clear();

            IsBusy = false;
            OnPropertyChanged(nameof(HasMore));
        }

        public async Task LoadAsync()
        {
            int generation = ++_generation;

            Servers.Clear();
            _ownDetails.Clear();
            _cursor = null;
            _failed = false;

            OnPropertyChanged(nameof(HasMore));

            if (!InGame || !SignedIn)
            {
                Refreshed();
                return;
            }

            long placeId = PlaceId;

            IsBusy = true;

            try
            {
                await LoadPageAsync(generation, placeId, null, true);
                _loadedPlaceId = placeId;
            }
            catch (Exception ex)
            {
                if (generation != _generation)
                    return;

                App.Logger.Error($"Failed to load the private servers of {placeId}");
                App.Logger.Error(ex);

                _failed = true;
            }
            finally
            {
                if (generation == _generation)
                    IsBusy = false;
            }
        }

        private async Task LoadMoreAsync()
        {
            if (String.IsNullOrEmpty(_cursor) || _isLoadingMore || !InGame)
                return;

            int generation = _generation;

            IsLoadingMore = true;

            try
            {
                await LoadPageAsync(generation, PlaceId, _cursor, false);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load more private servers");
                App.Logger.Error(ex);

                Flash(Strings.Menu_Overlay_PrivateServers_LoadFailed);
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private async Task LoadPageAsync(int generation, long placeId, string? cursor, bool first)
        {
            PrivateServersPage page = await PrivateServers.ListPageAsync(placeId, cursor);

            if (generation != _generation)
                return;

            long me = App.Cookies.CurrentUser?.Id ?? 0;

            string currentCode = _activityWatcher?.Data.ServerType == ServerType.Private
                ? _activityWatcher.Data.AccessCode
                : String.Empty;

            IEnumerable<PrivateServerItem> items = page.Data
                .Where(e => Servers.All(s => s.VipServerId != e.VipServerId))
                .Select(e => ToItem(e, me, currentCode));

            if (first)
                items = items.OrderByDescending(i => i.IsCurrent).ThenByDescending(i => i.IsMine);

            List<PrivateServerItem> added = items.ToList();

            foreach (PrivateServerItem item in added)
                Servers.Add(item);

            _cursor = page.NextPageCursor;

            OnPropertyChanged(nameof(HasMore));
            Refreshed();

            _ = LoadAvatarsAsync(added);

            foreach (PrivateServerItem item in added.Where(i => i.IsMine))
                _ = RefreshOwnAsync(item);
        }

        private static PrivateServerItem ToItem(PrivateServerEntry e, long me, string currentCode) => new()
        {
            VipServerId = e.VipServerId,
            AccessCode = e.AccessCode ?? String.Empty,
            Name = String.IsNullOrWhiteSpace(e.Name)
                ? Strings.Menu_Overlay_PrivateServers_Untitled
                : e.Name,
            OwnerId = e.Owner?.Id ?? 0,
            CapacityText = String.Format(Locale.CurrentCulture,
                Strings.Menu_Overlay_PrivateServers_Capacity, e.Playing ?? 0, e.MaxPlayers),
            IsMine = me != 0 && e.Owner?.Id == me,
            IsCurrent = !String.IsNullOrEmpty(currentCode) && currentCode == e.AccessCode
        };

        private async Task<UniverseDetails?> CurrentUniverseAsync()
        {
            ActivityData? activity = _activityWatcher?.Data;

            if (activity is null || activity.UniverseId == 0)
                return null;

            if (activity.UniverseDetails is null)
            {
                await UniverseDetails.FetchSingle(activity.UniverseId);
                activity.UniverseDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
            }

            return activity.UniverseDetails;
        }

        private static async Task LoadAvatarsAsync(IReadOnlyList<PrivateServerItem> items)
        {
            List<long> owners = items
                .Select(i => i.OwnerId)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            if (owners.Count == 0)
                return;

            try
            {
                Dictionary<long, UserDetails> details = await UserDetails.FetchBatch(owners);

                foreach (PrivateServerItem item in items)
                {
                    if (details.TryGetValue(item.OwnerId, out UserDetails? owner))
                        item.OwnerAvatar = owner.Thumbnail.ImageUrl;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Warn("Failed to load owner avatars");
                App.Logger.Error(ex);
            }
        }

        private async Task RefreshOwnAsync(PrivateServerItem item)
        {
            try
            {
                PrivateServerDetails details = await PrivateServers.DetailsAsync(item.VipServerId);

                _ownDetails[item.VipServerId] = details;

                item.IsActive = details.Active;
                item.Link = details.Link ?? String.Empty;
            }
            catch (Exception ex)
            {
                App.Logger.Warn("Failed to load a private server's settings");
                App.Logger.Error(ex);
            }
        }

        private async Task RowChangeAsync(
            PrivateServerItem? item,
            Func<PrivateServerDetails, Task> change,
            Func<PrivateServerDetails, string> success)
        {
            if (item is null || !item.IsMine || item.IsWorking)
                return;

            item.IsWorking = true;

            try
            {
                if (!_ownDetails.TryGetValue(item.VipServerId, out PrivateServerDetails? details))
                    details = await PrivateServers.DetailsAsync(item.VipServerId);

                string message = success(details);

                await change(details);

                Flash(message);
            }
            catch (SettingRejectedException ex) when (!String.IsNullOrWhiteSpace(ex.Reason))
            {
                App.Logger.Error(ex);

                Flash(String.Format(Locale.CurrentCulture,
                    Strings.Menu_Overlay_PrivateServers_Rejected, ex.Reason));
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to change the private server");
                App.Logger.Error(ex);

                Flash(Strings.Menu_Overlay_PrivateServers_SaveFailed);
            }

            await RefreshOwnAsync(item);

            item.IsWorking = false;
        }

        private void Join(PrivateServerItem? item)
        {
            if (item is null || !item.CanJoin)
                return;

            try
            {
                PrivateServers.Join(PlaceId, item.AccessCode);

                Flash(String.Format(Locale.CurrentCulture,
                    Strings.Menu_Overlay_Games_Joining, item.Name));
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to join a private server");
                App.Logger.Error(ex);
            }
        }

        private async Task ConfigureAsync(PrivateServerItem? item)
        {
            if (item is null || !item.IsMine)
                return;

            _managingId = item.VipServerId;
            _details = null;

            ServerName = item.Name;
            IsEditingName = false;
            IsAddingPeople = false;
            AddUsername = String.Empty;
            AllowedUsers.Clear();

            NotifyManage();

            IsBusy = true;

            try
            {
                UniverseDetails? universe = await CurrentUniverseAsync();

                GameName = universe?.Data.Name ?? String.Empty;
                GameIcon = universe?.Thumbnail?.ImageUrl;
            }
            catch (Exception ex)
            {
                App.Logger.Warn("Failed to load the experience");
                App.Logger.Error(ex);
            }

            try
            {
                await ReloadDetailsAsync();
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load the private server's settings");
                App.Logger.Error(ex);

                StopManaging();

                Flash(Strings.Menu_Overlay_PrivateServers_LoadFailed);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ReloadDetailsAsync()
        {
            if (_managingId is not long id)
                return;

            PrivateServerDetails details = await PrivateServers.DetailsAsync(id);

            if (_managingId != id)
                return;

            _details = details;
            _ownDetails[id] = details;

            ServerName = String.IsNullOrWhiteSpace(details.Name)
                ? Strings.Menu_Overlay_PrivateServers_Untitled
                : details.Name;

            if (!String.IsNullOrWhiteSpace(details.Game?.Name))
                GameName = details.Game.Name;

            AllowedUsers.Clear();

            foreach (PrivateServerUser user in details.Permissions?.Users ?? [])
                AllowedUsers.Add(user);

            NotifyManage();
        }

        private async Task<bool> ChangeAsync(Func<PrivateServerDetails, Task> change, string success)
        {
            if (_details is null || _isSaving)
                return false;

            bool changed = false;

            IsSaving = true;

            try
            {
                await change(_details);

                changed = true;

                Flash(success);
            }
            catch (SettingRejectedException ex) when (!String.IsNullOrWhiteSpace(ex.Reason))
            {
                App.Logger.Error(ex);

                Flash(String.Format(Locale.CurrentCulture,
                    Strings.Menu_Overlay_PrivateServers_Rejected, ex.Reason));
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to change the private server");
                App.Logger.Error(ex);

                Flash(Strings.Menu_Overlay_PrivateServers_SaveFailed);
            }

            try
            {
                await ReloadDetailsAsync();
            }
            catch (Exception ex)
            {
                App.Logger.Warn("Failed to reload the private server's settings");
                App.Logger.Error(ex);
            }

            IsSaving = false;

            NotifyManage();

            return changed;
        }

        private async Task SaveNameAsync()
        {
            string name = EditName.Trim();

            if (name.Length == 0 || name == _details?.Name)
            {
                IsEditingName = false;
                return;
            }

            if (await ChangeAsync(d => PrivateServers.RenameAsync(d, name),
                    Strings.Menu_Overlay_PrivateServers_Saved))
            {
                IsEditingName = false;
            }
        }

        private void CopyToClipboard(string? link)
        {
            if (String.IsNullOrEmpty(link))
                return;

            try
            {
                var lifetime = App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var clipboard = lifetime?.MainWindow?.Clipboard;

                if (clipboard is null)
                    return;

                _ = clipboard.SetTextAsync(link);

                Flash(Strings.Menu_Overlay_PrivateServers_LinkCopied);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to copy the link");
                App.Logger.Error(ex);
            }
        }

        private async Task AddUserAsync()
        {
            string username = AddUsername.Trim().TrimStart('@');

            if (username.Length == 0 || _details is null || _isSaving)
                return;

            PrivateServerUser? user;

            IsSaving = true;

            try
            {
                user = await PrivateServers.FindUserAsync(username);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to look the user up");
                App.Logger.Error(ex);

                Flash(Strings.Menu_Overlay_PrivateServers_SaveFailed);
                return;
            }
            finally
            {
                IsSaving = false;
            }

            if (user is null)
            {
                Flash(String.Format(Locale.CurrentCulture,
                    Strings.Menu_Overlay_PrivateServers_NoSuchUser, username));
                return;
            }

            string name = user.Name ?? username;

            if (AllowedUsers.Any(u => u.Id == user.Id))
            {
                Flash(String.Format(Locale.CurrentCulture,
                    Strings.Menu_Overlay_PrivateServers_AlreadyAdded, name));
                return;
            }

            if (await ChangeAsync(d => PrivateServers.AddUserAsync(d, user),
                    String.Format(Locale.CurrentCulture,
                        Strings.Menu_Overlay_PrivateServers_Added, name)))
            {
                AddUsername = String.Empty;
                IsAddingPeople = false;
            }
        }

        private async Task RemoveUserAsync(PrivateServerUser? user)
        {
            if (user is null)
                return;

            await ChangeAsync(
                d => PrivateServers.RemoveUserAsync(d, user),
                String.Format(Locale.CurrentCulture,
                    Strings.Menu_Overlay_PrivateServers_Removed,
                    user.Name ?? user.Id.ToString(CultureInfo.InvariantCulture)));
        }

        private void Back()
        {
            StopManaging();
            _ = LoadAsync();
        }

        private void StopManaging()
        {
            _managingId = null;
            _details = null;

            IsEditingName = false;
            IsAddingPeople = false;
            AllowedUsers.Clear();

            NotifyManage();
        }

        private void NotifyManage()
        {
            OnPropertyChanged(nameof(IsManaging));
            OnPropertyChanged(nameof(ListVisible));
            OnPropertyChanged(nameof(ManageVisible));
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(FriendsAllowed));
            OnPropertyChanged(nameof(Link));
            OnPropertyChanged(nameof(HasLink));
            OnPropertyChanged(nameof(PriceText));
            OnPropertyChanged(nameof(RenewalText));
            OnPropertyChanged(nameof(HasRenewal));
            OnPropertyChanged(nameof(HasNoAllowedUsers));
            OnPropertyChanged(nameof(HasMore));

            Refreshed();
        }

        private void Flash(string message)
        {
            _status = message;

            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(HasStatus));

            _statusTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _statusTimer.Tick -= ClearStatus;
            _statusTimer.Tick += ClearStatus;

            _statusTimer.Stop();
            _statusTimer.Start();
        }

        private void ClearStatus(object? sender, EventArgs e)
        {
            _statusTimer?.Stop();

            _status = null;

            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(HasStatus));
        }

        private void Refreshed()
        {
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(EmptyText));
        }
    }
}