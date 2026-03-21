using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Models.APIs.Roblox;
using Froststrap.UI.Elements.Base;

namespace Froststrap.UI.ViewModels.Dialogs
{
    public partial class ManualCookieDialogViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _cookieInput = string.Empty;

        [ObservableProperty]
        private bool _isValidating;

        [ObservableProperty]
        private bool _isAddEnabled = true;

        public AccountManagerAccount? ValidatedAccount { get; private set; }

        private readonly AvaloniaWindow _window;

        public ManualCookieDialogViewModel(AvaloniaWindow window)
        {
            _window = window;
        }

        [RelayCommand]
        private async Task AddAccountAsync()
        {
            if (string.IsNullOrWhiteSpace(CookieInput))
            {
                await Frontend.ShowMessageBox("Please enter a cookie.", MessageBoxImage.Warning);
                return;
            }

            IsValidating = true;
            IsAddEnabled = false;

            try
            {
                var accountInfo = await GetAccountInfoFromCookieAsync(CookieInput);

                if (accountInfo == null)
                {
                    await Frontend.ShowMessageBox("Invalid cookie. Please check and try again.", MessageBoxImage.Error);
                    return;
                }

                ValidatedAccount = accountInfo;

                _window.Close(ValidatedAccount);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ManualCookieDialog", $"Validation error: {ex.Message}");
                await Frontend.ShowMessageBox($"Error validating cookie: {ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                IsValidating = false;
                IsAddEnabled = true;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            _window.Close(null);
        }

        private async Task<AccountManagerAccount?> GetAccountInfoFromCookieAsync(string cookie)
        {
            try
            {
                string cleanCookie = cookie.Trim();
                if (!cleanCookie.Contains(".ROBLOSECURITY="))
                {
                    cleanCookie = $".ROBLOSECURITY={cleanCookie}";
                }

                var cookieContainer = new CookieContainer();
                using var handler = new HttpClientHandler { CookieContainer = cookieContainer };
                using var client = new HttpClient(handler);

                cookieContainer.Add(new Uri("https://roblox.com"), new Cookie(".ROBLOSECURITY", cookie, "/", ".roblox.com"));

                var response = await client.GetAsync("https://users.roblox.com/v1/users/authenticated");

                if (!response.IsSuccessStatusCode)
                    return null;

                var user = await response.Content.ReadFromJsonAsync<AuthenticatedUser>();

                if (user == null || user.Id == 0)
                    return null;

                return new AccountManagerAccount(cookie, user.Id, user.Username, user.Displayname);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ManualCookieDialog", $"HTTP Error: {ex.Message}");
                return null;
            }
        }
    }
}