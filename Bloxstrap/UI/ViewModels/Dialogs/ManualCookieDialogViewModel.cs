using Bloxstrap.Integrations;
using Bloxstrap.UI.Elements.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using System.Windows;

namespace Bloxstrap.UI.ViewModels.Dialogs
{
    public partial class ManualCookieDialogViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _cookieInput = "";

        [ObservableProperty]
        private bool _isValidating = false;

        [ObservableProperty]
        private bool _isAddEnabled = true;

        public AltAccount? ValidatedAccount { get; private set; }

        private ManualCookieDialog _dialog;

        public ManualCookieDialogViewModel(ManualCookieDialog dialog)
        {
            _dialog = dialog;
        }

        [RelayCommand]
        private async Task AddAccount()
        {
            if (string.IsNullOrWhiteSpace(CookieInput))
            {
                Frontend.ShowMessageBox("Please enter a cookie.", MessageBoxImage.Warning);
                return;
            }

            IsValidating = true;
            IsAddEnabled = false;

            try
            {
                var accountInfo = await GetAccountInfoFromCookieAsync(CookieInput);

                if (accountInfo == null)
                {
                    Frontend.ShowMessageBox("Invalid cookie. Please check and try again.", MessageBoxImage.Error);
                    return;
                }

                ValidatedAccount = accountInfo;
                _dialog.DialogResult = true;
                _dialog.Close();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ManualCookieDialog", $"Validation error: {ex.Message}");
                Frontend.ShowMessageBox($"Error validating cookie: {ex.Message}", MessageBoxImage.Error);
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
            _dialog.DialogResult = false;
            _dialog.Close();
        }

        private async Task<AltAccount?> GetAccountInfoFromCookieAsync(string cookie)
        {
            try
            {
                using var handler = new System.Net.Http.HttpClientHandler
                {
                    CookieContainer = new System.Net.CookieContainer()
                };
                handler.CookieContainer.Add(new System.Net.Cookie(".ROBLOSECURITY", cookie, "/", ".roblox.com"));

                using var client = new System.Net.Http.HttpClient(handler);
                var response = await client.GetAsync("https://users.roblox.com/v1/users/authenticated");

                if (!response.IsSuccessStatusCode)
                    return null;

                string json = await response.Content.ReadAsStringAsync();
                var jo = JObject.Parse(json);

                long userId = jo["id"]?.Value<long>() ?? 0;
                string username = jo["name"]?.Value<string>() ?? string.Empty;
                string displayName = jo["displayName"]?.Value<string>() ?? string.Empty;

                if (userId == 0 || string.IsNullOrEmpty(username))
                    return null;

                return new AltAccount(cookie, userId, username, displayName);
            }
            catch
            {
                return null;
            }
        }
    }
}