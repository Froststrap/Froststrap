using System.Net.Http.Json;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.Elements.Base;

namespace Froststrap.UI.ViewModels.Dialogs
{
    public partial class ManualCookieDialogViewModel(AvaloniaWindow window) : NotifyPropertyChangedViewModel
    {
        private string _cookieInput = string.Empty;
        public string CookieInput
        {
            get => _cookieInput;
            set => SetProperty(ref _cookieInput, value);
        }

        private bool _isValidating;
        public bool IsValidating
        {
            get => _isValidating;
            set => SetProperty(ref _isValidating, value);
        }

        private bool _isAddEnabled = true;
        public bool IsAddEnabled
        {
            get => _isAddEnabled;
            set => SetProperty(ref _isAddEnabled, value);
        }

        public AccountManagerAccount? ValidatedAccount { get; private set; }

        [RelayCommand]
        private async Task AddAccountAsync()
        {
            if (string.IsNullOrWhiteSpace(CookieInput))
            {
                await Frontend.ShowMessageBox(Strings.Menu_ManualLogin_EnterCookie, MessageBoxImage.Warning);
                return;
            }

            IsValidating = true;
            IsAddEnabled = false;

            try
            {
                var accountInfo = await GetAccountInfoFromCookieAsync(CookieInput);

                if (accountInfo == null)
                {
                    await Frontend.ShowMessageBox(Strings.Menu_ManualLogin_InvalidCookie, MessageBoxImage.Error);
                    return;
                }

                ValidatedAccount = accountInfo;
                window.Close(ValidatedAccount);
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
        private void Cancel() => window.Close(null);

        private static async Task<AccountManagerAccount?> GetAccountInfoFromCookieAsync(string cookie)
        {
            try
            {
                var cookieContainer = new CookieContainer();
                using HttpClientHandler handler = new() { CookieContainer = cookieContainer };
                using HttpClient client = new(handler);

                string rawValue = cookie.Contains(".ROBLOSECURITY=")
                    ? cookie.Split(".ROBLOSECURITY=")[1].Split(';')[0].Trim()
                    : cookie.Trim();

                cookieContainer.Add(new Uri("https://roblox.com"), new Cookie(".ROBLOSECURITY", rawValue, "/", ".roblox.com"));

                var response = await client.GetAsync(UrlBuilder.BuildApiUrl("users", "v1/users/authenticated", secure: true));

                if (!response.IsSuccessStatusCode)
                    return null;

                var user = await response.Content.ReadFromJsonAsync<AuthenticatedUser>();

                if (user is not { Id: > 0 })
                    return null;

                return new AccountManagerAccount(rawValue, user.Id, user.Username, user.DisplayName);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ManualCookieDialog", $"HTTP Error: {ex.Message}");
                return null;
            }
        }
    }
}