/*
 *  Froststrap
 *  Copyright (c) Froststrap Team
 *
 *  This file is part of Froststrap and is distributed under the terms of the
 *  GNU Affero General Public License, version 3 or later.
 *
 *  SPDX-License-Identifier: AGPL-3.0-or-later
 *
 *  Description: Nix flake for shipping for Nix-darwin, Nix, NixOS, and modules
 *               of the Nix ecosystem. 
 */

using Bloxstrap.Integrations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace Bloxstrap.UI.ViewModels.AccountManagers
{
    public record Account(long Id, string DisplayName, string Username, string? AvatarUrl);
    public record AccountPresence(int UserPresenceType, string LastLocation, string StatusColor, string ToolTipText);

    public partial class AccountsViewModel : ObservableObject
    {
        private const string LOG_IDENT = "AccountsViewModel";

        [ObservableProperty]
        private string _currentUserDisplayName = "Not Logged In";

        [ObservableProperty]
        private string _currentUserUsername = "";

        [ObservableProperty]
        private string _currentUserAvatarUrl = "";

        [ObservableProperty]
        private ObservableCollection<Account> _accounts = new();

        [ObservableProperty]
        private Account? _selectedAccount;

        [ObservableProperty]
        private Account? _draggedAccount;

        [ObservableProperty]
        private AccountPresence? _currentUserPresence;

        [ObservableProperty]
        private bool _isAccountInformationVisible;

        [ObservableProperty]
        private int _friendsCount;

        [ObservableProperty]
        private int _followersCount;

        [ObservableProperty]
        private int _followingCount;

        [ObservableProperty]
        private ObservableCollection<string> _addMethods = new(new[] { "Quick Sign-In", "Browser" });

        [ObservableProperty]
        private string _selectedAddMethod = "Quick Sign-In";

        private AccountManager Manager => AccountManager.Shared;

        public static long? GetActiveUserId()
        {
            try
            {
                return AccountManager.Shared.ActiveAccount?.UserId;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::GetActiveUserId", $"Exception: {ex.Message}");
                return null;
            }
        }

        public AccountsViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return;

            _ = InitializeDataAsync();
        }

        private async Task InitializeDataAsync()
        {
            try
            {
                await LoadDataAsync();
                App.Logger.WriteLine($"{LOG_IDENT}::InitializeDataAsync", "Loaded");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::InitializeDataAsync", $"Exception: {ex.Message}");
                CurrentUserDisplayName = "Error Loading";
                CurrentUserUsername = "Failed to load account data";
            }
        }

        private async Task LoadDataAsync()
        {
            Accounts.Clear();

            var mgr = Manager;

            // Load all accounts
            var accountTasks = mgr.Accounts.Select(async acc =>
            {
                string? avatarUrl = await GetAvatarUrl(acc.UserId);
                return new Account(acc.UserId, acc.DisplayName, acc.Username, string.IsNullOrEmpty(avatarUrl) ? null : avatarUrl);
            }).ToList();

            var accountResults = await Task.WhenAll(accountTasks);
            foreach (var account in accountResults)
            {
                Accounts.Add(account);
            }

            // Set current account info
            if (mgr.ActiveAccount is not null)
            {
                CurrentUserDisplayName = mgr.ActiveAccount.DisplayName;
                CurrentUserUsername = $"@{mgr.ActiveAccount.Username}";

                string? avatarUrl = await GetAvatarUrl(mgr.ActiveAccount.UserId);
                CurrentUserAvatarUrl = avatarUrl ?? "";

                SelectedAccount = Accounts.FirstOrDefault(a => a.Id == mgr.ActiveAccount.UserId);

                await UpdateAccountInformationAsync(mgr.ActiveAccount.UserId);
            }
            else
            {
                CurrentUserDisplayName = "Not Logged In";
                CurrentUserUsername = "";
                CurrentUserAvatarUrl = "";
                IsAccountInformationVisible = false;
            }
        }

        private async Task<string?> GetAvatarUrl(long userId)
        {
            try
            {
                var request = new ThumbnailRequest
                {
                    TargetId = (ulong)userId,
                    Type = "AvatarHeadShot",
                    Size = "75x75",
                    Format = "Png",
                    IsCircular = true
                };

                return await Thumbnails.GetThumbnailUrlAsync(request, CancellationToken.None);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::GetAvatarUrl", $"Exception: {ex.Message}");
                return null;
            }
        }

        private async Task<(int friends, int followers, int following)> GetAccountInformationAsync(long userId)
        {
            if (userId == 0)
                return (0, 0, 0);

            try
            {
                using var client = new HttpClient();

                var friendsTask = client.GetAsync($"https://friends.roblox.com/v1/users/{userId}/friends/count");
                var followersTask = client.GetAsync($"https://friends.roblox.com/v1/users/{userId}/followers/count");
                var followingTask = client.GetAsync($"https://friends.roblox.com/v1/users/{userId}/followings/count");

                await Task.WhenAll(friendsTask, followersTask, followingTask);

                async Task<int> ParseCount(HttpResponseMessage response)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content);
                        return json.GetProperty("count").GetInt32();
                    }
                    return 0;
                }

                var friendsCount = await ParseCount(friendsTask.Result);
                var followersCount = await ParseCount(followersTask.Result);
                var followingCount = await ParseCount(followingTask.Result);

                return (friendsCount, followersCount, followingCount);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::GetAccountInformation", $"Exception: {ex.Message}");
                return (0, 0, 0);
            }
        }

        private async Task UpdateAccountInformationAsync(long userId)
        {
            if (userId == 0)
            {
                IsAccountInformationVisible = false;
                return;
            }

            try
            {
                var (friends, followers, following) = await GetAccountInformationAsync(userId);

                FriendsCount = friends;
                FollowersCount = followers;
                FollowingCount = following;

                IsAccountInformationVisible = true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::UpdateAccountInformation", $"Exception: {ex.Message}");
                IsAccountInformationVisible = false;
            }
        }

        private async Task SwitchToAccountAsync(AltAccount account)
        {
            CurrentUserDisplayName = account.DisplayName;
            CurrentUserUsername = $"@{account.Username}";

            string? avatarUrl = await GetAvatarUrl(account.UserId);
            CurrentUserAvatarUrl = avatarUrl ?? "";

            await UpdateAccountInformationAsync(account.UserId);
        }

        [RelayCommand]
        private async Task SelectAccount()
        {
            if (SelectedAccount is null)
            {
                Frontend.ShowMessageBox("Please select an account first.", MessageBoxImage.Warning);
                return;
            }

            var mgr = Manager;
            bool isSameAccount = mgr.ActiveAccount?.UserId == SelectedAccount.Id;

            var backendAccount = mgr.Accounts.FirstOrDefault(acc => acc.UserId == SelectedAccount.Id);
            if (backendAccount is not null)
            {
                if (!isSameAccount)
                {
                    mgr.SetActiveAccount(backendAccount);
                    await SwitchToAccountAsync(backendAccount);
                    Frontend.ShowMessageBox($"Switched to account: {SelectedAccount.DisplayName}", MessageBoxImage.Information);
                }
                else
                {
                    Frontend.ShowMessageBox($"{SelectedAccount.DisplayName} is already the active account.", MessageBoxImage.Information);
                }
            }
        }

        [RelayCommand]
        private async Task AddAccount()
        {
            var mgr = Manager;
            AltAccount? newAccount = null;

            try
            {
                if (string.Equals(SelectedAddMethod, "Quick Sign-In", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.WriteLine($"{LOG_IDENT}::AddAccount", "Adding account via Quick Sign-In");
                    newAccount = await mgr.AddAccountByQuickSignInAsync();

                    if (newAccount is null)
                    {
                        Frontend.ShowMessageBox("Quick Sign-In was cancelled or failed. Please try again or use browser login.", MessageBoxImage.Information);
                        return;
                    }
                }
                else
                {
                    App.Logger.WriteLine($"{LOG_IDENT}::AddAccount", "Adding account via Browser");
                    newAccount = await mgr.AddAccountByBrowser();
                }

                if (newAccount is not null)
                {
                    if (!Accounts.Any(a => a.Id == newAccount.UserId))
                    {
                        Accounts.Add(new Account(newAccount.UserId, newAccount.DisplayName, newAccount.Username, ""));

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                string? avatarUrl = await GetAvatarUrl(newAccount.UserId);
                                var updatedAccount = new Account(newAccount.UserId, newAccount.DisplayName, newAccount.Username, avatarUrl);

                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    var existingAccount = Accounts.FirstOrDefault(a => a.Id == newAccount.UserId);
                                    if (existingAccount != null)
                                    {
                                        var index = Accounts.IndexOf(existingAccount);
                                        Accounts[index] = updatedAccount;
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                App.Logger.WriteLine($"{LOG_IDENT}::AddAccount::AvatarLoad", $"Exception: {ex.Message}");
                            }
                        });
                    }

                    mgr.SetActiveAccount(newAccount);
                    await SwitchToAccountAsync(newAccount);

                    Frontend.ShowMessageBox($"Added and switched to account: {newAccount.DisplayName}", MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::AddAccount", $"Exception: {ex.Message}");
                Frontend.ShowMessageBox($"Failed to add account: {ex.Message}", MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteAccount(Account? account)
        {
            var mgr = Manager;
            var target = account ?? SelectedAccount;
            if (target is null)
            {
                Frontend.ShowMessageBox("Please select an account to delete.", MessageBoxImage.Warning);
                return;
            }

            var backendAccount = mgr.Accounts.FirstOrDefault(acc => acc.UserId == target.Id);
            if (backendAccount is null)
            {
                Frontend.ShowMessageBox("Selected account could not be found in the backend.", MessageBoxImage.Error);
                return;
            }

            var result = Frontend.ShowMessageBox(
                $"Delete account '{target.DisplayName}' (@{target.Username})?",
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );
            if (result != MessageBoxResult.Yes) return;

            bool isDeletingActiveAccount = mgr.ActiveAccount?.UserId == target.Id;

            bool removed = mgr.RemoveAccount(backendAccount);
            if (!removed)
            {
                Frontend.ShowMessageBox("Failed to delete account.", MessageBoxImage.Error);
                return;
            }

            var uiAccount = Accounts.FirstOrDefault(a => a.Id == target.Id);
            if (uiAccount != null) Accounts.Remove(uiAccount);

            if (isDeletingActiveAccount)
            {
                mgr.SetActiveAccount(null);
                CurrentUserDisplayName = "Not Logged In";
                CurrentUserUsername = "";
                CurrentUserAvatarUrl = "";
                IsAccountInformationVisible = false;
            }

            var currentActiveAccount = mgr.ActiveAccount;
            if (currentActiveAccount != null)
            {
                SelectedAccount = Accounts.FirstOrDefault(a => a.Id == currentActiveAccount.UserId);
            }
            else
            {
                SelectedAccount = null;
            }

            App.Logger.WriteLine($"{LOG_IDENT}::DeleteAccount", $"Account '{target.DisplayName}' deleted successfully");
        }

        [RelayCommand]
        private void SignOut()
        {
            var mgr = Manager;
            mgr.SetActiveAccount(null);
            CurrentUserDisplayName = "Not Logged In";
            CurrentUserUsername = "";
            CurrentUserAvatarUrl = "";

            FriendsCount = 0;
            FollowersCount = 0;
            FollowingCount = 0;
            IsAccountInformationVisible = false;

            Frontend.ShowMessageBox("Signed out successfully.", MessageBoxImage.Information);
        }
    }
}