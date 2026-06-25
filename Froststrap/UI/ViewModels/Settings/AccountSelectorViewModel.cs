using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using Froststrap.UI.Elements.Dialogs;
using System.Collections.ObjectModel;
using System.Security.Principal;

namespace Froststrap.UI.ViewModels.Settings
{
    public partial class AccountSelectorViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private const string LOG_IDENT = "AccountSelectorViewModel";
        private readonly AccountManager _accountManager = null!;
        private readonly Dictionary<long, string?> _accountAvatarUrls = [];

        public event Action? OnManualAddRequested;

        private DispatcherTimer? _presenceTimer;
        private UserPresence? _currentPresence;
        private const int PRESENCE_REFRESH_INTERVAL_MS = 20000;

        private AccountManagerAccount? _currentAccount;
        public AccountManagerAccount? CurrentAccount
        {
            get => _currentAccount;
            set => SetProperty(ref _currentAccount, value);
        }

        private string? _currentAccountAvatarUrl;
        public string? CurrentAccountAvatarUrl
        {
            get => _currentAccountAvatarUrl;
            set => SetProperty(ref _currentAccountAvatarUrl, value);
        }

        private ObservableCollection<AccountWithAvatar> _accounts = [];
        public ObservableCollection<AccountWithAvatar> Accounts
        {
            get => _accounts;
            set => SetProperty(ref _accounts, value);
        }

        private string _selectedAddMethod = Strings.Menu_AccountSelector_Login_LocalCookie;
        public string SelectedAddMethod
        {
            get => _selectedAddMethod;
            set => SetProperty(ref _selectedAddMethod, value);
        }

        private bool _isDropdownOpen;
        public bool IsDropdownOpen
        {
            get => _isDropdownOpen;
            set => SetProperty(ref _isDropdownOpen, value);
        }

        private bool _isAddingAccount;
        public bool IsAddingAccount
        {
            get => _isAddingAccount;
            set => SetProperty(ref _isAddingAccount, value);
        }

        private UserPresence? CurrentPresence
        {
            get => _currentPresence;
            set
            {
                if (SetProperty(ref _currentPresence, value))
                {
                    OnPropertyChanged(nameof(PresenceTooltip));
                    UpdatePresenceBrush();
                }
            }
        }

        private IBrush _presenceBrush = Brushes.Gray;
        public IBrush PresenceBrush
        {
            get => _presenceBrush;
            private set => SetProperty(ref _presenceBrush, value);
        }

        private void UpdatePresenceBrush()
        {
            if (CurrentPresence == null)
            {
                PresenceBrush = Brushes.Gray;
                return;
            }

            int type = CurrentPresence.UserPresenceType;
            PresenceBrush = type switch
            {
                0 => Brushes.Gray,
                1 => Brushes.DodgerBlue,
                2 => Brushes.LimeGreen,
                3 => Brushes.Orange,
                _ => Brushes.Gray
            };
        }

        public string PresenceTooltip
        {
            get
            {
                if (CurrentPresence == null) return "Offline";
                return CurrentPresence.UserPresenceType switch
                {
                    0 => Strings.Menu_AccountSelector_Presence_Offline,
                    1 => Strings.Menu_AccountSelector_Presence_Online,
                    2 => Strings.Menu_AccountSelector_Presence_InGame,
                    3 => Strings.Menu_AccountSelector_Presence_InStudio,
                    _ => Strings.Common_Unknown
                };
            }
        }

        public List<string> AddMethods { get; } = [Strings.Menu_AccountSelector_Login_LocalCookie, Strings.Menu_Dialog_QuickSignIn_Title, Strings.Common_Manual, Strings.Common_Browser];

        public string CurrentAccountDisplayName => CurrentAccount == null ? Strings.Menu_AccountSelector_NotLoggedIn : $"@{CurrentAccount.Username}";

        public AccountSelectorViewModel()
        {
            if (Design.IsDesignMode)
                return;

            _accountManager = AccountManager.Shared;
            _accountManager.ActiveAccountChanged += OnActiveAccountChanged;

            Task.Run(async () =>
            {
                try
                {
                    await InitializeDataAsync();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine($"{LOG_IDENT}::Init", $"Safe catch: {ex.Message}");
                }
            });
        }

        private async Task InitializeDataAsync()
        {
            try
            {
                await LoadDataAsync();
                await ValidateAndRemoveInvalidAccountsAsync();
                App.Logger.WriteLine($"{LOG_IDENT}::InitializeDataAsync", "Initialised");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::InitializeDataAsync", $"Exception: {ex.Message}");
            }
        }

        private async Task ValidateAndRemoveInvalidAccountsAsync()
        {
            var invalidAccounts = new List<AccountManagerAccount>();

            foreach (var account in _accountManager.Accounts)
            {
                bool isValid = await AccountManager.ValidateAccountAsync(account);
                if (!isValid)
                    invalidAccounts.Add(account);
            }

            foreach (var account in invalidAccounts)
            {
                _accountManager.RemoveAccount(account);
                App.Logger.WriteLine(LOG_IDENT, $"Removed expired/invalid account: {account.Username}");

                await Dispatcher.UIThread.InvokeAsync(() => Frontend.ShowMessageBox(string.Format(Strings.Menu_AccountSelector_AccountRemoved, account.Username)));
            }

            if (invalidAccounts.Count > 0)
            {
                await LoadDataAsync();
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var mgr = _accountManager;
                var accountIds = mgr.Accounts.Select(acc => acc.UserId).ToList();

                if (accountIds.Count > 0)
                {
                    var avatarUrls = await mgr.GetAvatarUrlsBulkAsync(accountIds);
                    foreach (var kvp in avatarUrls)
                    {
                        _accountAvatarUrls[kvp.Key] = kvp.Value;
                    }
                }

                CurrentAccount = mgr.ActiveAccount;
                if (CurrentAccount != null)
                {
                    CurrentAccountAvatarUrl = GetAccountAvatarUrl(CurrentAccount.UserId);
                }
                OnPropertyChanged(nameof(CurrentAccountDisplayName));

                Accounts.Clear();
                foreach (var account in mgr.Accounts)
                {
                    var url = _accountAvatarUrls.TryGetValue(account.UserId, out var u) ? u : null;
                    Accounts.Add(new AccountWithAvatar(account, url));
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to load account data: {ex.Message}");
            }
        }

        private void OnActiveAccountChanged(AccountManagerAccount? account)
        {
            Dispatcher.UIThread.Post(() =>
            {
                CurrentAccount = account;
                CurrentAccountAvatarUrl = account != null ? GetAccountAvatarUrl(account.UserId) : null;
                OnPropertyChanged(nameof(CurrentAccountDisplayName));

                StopPresencePolling();
                if (account != null)
                {
                    StartPresencePolling();
                    _ = RefreshPresenceAsync();
                }
                else
                {
                    CurrentPresence = null;
                }
            });
        }

        private void StartPresencePolling()
        {
            if (_presenceTimer != null) return;

            _presenceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(PRESENCE_REFRESH_INTERVAL_MS)
            };
            _presenceTimer.Tick += async (s, e) => await RefreshPresenceAsync();
            _presenceTimer.Start();
        }

        private void StopPresencePolling()
        {
            _presenceTimer?.Stop();
            _presenceTimer = null;
        }

        private async Task RefreshPresenceAsync()
        {
            if (CurrentAccount == null) return;

            try
            {
                var presence = await AccountManager.GetUserPresenceAsync(CurrentAccount.UserId);
                if (presence != null)
                {
                    CurrentPresence = presence;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to refresh presence: {ex.Message}");
            }
        }


        [RelayCommand]
        private void SelectAccount(AccountWithAvatar item)
        {
            _accountManager.SetActiveAccount(item.UserId);
            IsDropdownOpen = false;
        }

        [RelayCommand]
        private void ToggleDropdown() => IsDropdownOpen = !IsDropdownOpen;

        [RelayCommand]
        private void DeleteAccount(AccountWithAvatar item)
        {
            _accountManager.RemoveAccount(item.Account);
            Accounts.Remove(item);
        }

        [RelayCommand]
        private async Task AddAccount()
        {
            IsAddingAccount = true;

            try
            {
                AccountManagerAccount? newAccount = null;

                if (SelectedAddMethod == Strings.Menu_Dialog_QuickSignIn_Title)
                {
                    var dialog = new QuickSignCodeDialog();
                    var cts = new CancellationTokenSource();

                    dialog.Closed += (_, _) => cts.Cancel();
                    dialog.Show();

                    newAccount = await AccountManager.AddAccountByQuickSignInAsync(dialog, cts.Token);
                }
                else if (SelectedAddMethod == Strings.Common_Browser)
                {
                    newAccount = await _accountManager.AddAccountByBrowser();
                }
                else if (SelectedAddMethod == Strings.Common_Manual)
                {
                    OnManualAddRequested?.Invoke();
                    return;
                }
                else if (SelectedAddMethod == Strings.Menu_AccountSelector_Login_LocalCookie)
                {
                    newAccount = await ImportFromCookieManager();
                }

                if (newAccount == null) return;

                if (await HandleDuplicateAccountAsync(newAccount))
                    return;

                _accountManager.AddAccount(newAccount);

                var avatarUrlMap = await _accountManager.GetAvatarUrlsBulkAsync([newAccount.UserId]);
                var url = avatarUrlMap.GetValueOrDefault(newAccount.UserId);
                _accountAvatarUrls[newAccount.UserId] = url;

                Accounts.Add(new AccountWithAvatar(newAccount, url));
                _accountManager.SetActiveAccount(newAccount.UserId);
                IsDropdownOpen = false;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Error adding account: {ex.Message}");
            }
            finally
            {
                IsAddingAccount = false;
            }
        }

        private async Task<bool> HandleDuplicateAccountAsync(AccountManagerAccount account)
        {
            var existing = _accountManager.Accounts.FirstOrDefault(a => a.UserId == account.UserId);
            if (existing != null)
            {
                _accountManager.SetActiveAccount(existing.UserId);
                IsDropdownOpen = false;
                await Frontend.ShowMessageBox(string.Format(Strings.Menu_AccountSelector_AccountRemoved, existing.Username), MessageBoxImage.Information);
                return true;
            }
            return false;
        }

        private static async Task<AccountManagerAccount?> ImportFromCookieManager()
        {
            const string LOG_IDENT = "ImportFromCookieManager";

            var cookieManager = new CookiesManager();

            await cookieManager.LoadCookies();

            if (!cookieManager.Loaded)
            {
                string error = cookieManager.State switch
                {
                    CookieState.NotAllowed => Strings.Menu_CookieState_NotAllowed,
                    CookieState.NotFound => Strings.Menu_CookieState_NotFound,
                    CookieState.Invalid => Strings.Menu_CookieState_Invalid,
                    CookieState.Failed => Strings.Menu_CookieState_Failed,
                    _ => Strings.Menu_CookieState_CouldNotLoad
                };
                _ = Frontend.ShowMessageBox(error);
                return null;
            }

            var authUser = await cookieManager.GetAuthenticated();
            if (authUser == null || authUser.Id == 0)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to get authenticated user from cookie.");
                return null;
            }

            string cookieValue = cookieManager.GetAuthCookie();
            if (string.IsNullOrEmpty(cookieValue))
            {
                App.Logger.WriteLine(LOG_IDENT, "Auth cookie is empty.");
                return null;
            }

            return new AccountManagerAccount(
                securityToken: cookieValue,
                userId: authUser.Id,
                username: authUser.Username,
                displayName: authUser.DisplayName
            );
        }

        public async void AddAccountDirect(AccountManagerAccount account)
        {
            if (await HandleDuplicateAccountAsync(account))
                return;

            _accountManager.AddAccount(account);

            string? avatarUrl = null;
            try
            {
                var urlMap = await _accountManager.GetAvatarUrlsBulkAsync([account.UserId]);
                avatarUrl = urlMap.GetValueOrDefault(account.UserId);
                if (avatarUrl != null)
                    _accountAvatarUrls[account.UserId] = avatarUrl;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to fetch avatar for {account.UserId}: {ex.Message}");
            }

            if (Accounts.All(a => a.UserId != account.UserId))
            {
                Accounts.Add(new AccountWithAvatar(account, avatarUrl));
            }

            _accountManager.SetActiveAccount(account.UserId);
            IsDropdownOpen = false;
        }

        public class AccountWithAvatar(AccountManagerAccount account, string? avatarUrl)
        {
            public AccountManagerAccount Account { get; } = account;
            public string? AvatarUrl { get; } = avatarUrl;

            public string Username => Account.Username;
            public string DisplayName => Account.DisplayName;
            public long UserId => Account.UserId;
        }

        public string? GetAccountAvatarUrl(long userId)
        {
            return _accountAvatarUrls.TryGetValue(userId, out var url)
                ? url
                : _accountManager.GetCachedAvatarUrl(userId);
        }

        public void Dispose()
        {
            StopPresencePolling();
            _accountManager.ActiveAccountChanged -= OnActiveAccountChanged;
            GC.SuppressFinalize(this);
        }
    }
}