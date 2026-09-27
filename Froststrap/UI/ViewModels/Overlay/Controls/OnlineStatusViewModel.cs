using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Froststrap.RobloxInterfaces;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    internal class OnlineStatusOption
    {
        public string Value { get; init; } = String.Empty;

        public string Label { get; init; } = String.Empty;

        public bool IsCurrent { get; init; }
    }

    internal class OnlineStatusViewModel : NotifyPropertyChangedViewModel
    {
        private string? _online;

        private string? _join;

        private bool _busy;

        private string? _status;

        private bool _isOpen;

        public bool IsOpen
        {
            get => _isOpen;
            set
            {
                if (_isOpen == value)
                    return;

                _isOpen = value;

                OnPropertyChanged(nameof(IsOpen));

                if (value)
                    _ = LoadAsync();
            }
        }

        public IReadOnlyList<OnlineStatusOption> Options =>
        [
            .. PrivacySettings.OnlineLevels
                .Select(x => new OnlineStatusOption { Value = x, Label = LabelFor(x), IsCurrent = x == _online })
        ];

        public bool CanChange => !_busy && _online is not null;

        public string StatusText => _status ?? String.Empty;

        public bool HasStatus => _status is not null;

        public ICommand OpenCommand { get; }
        public ICommand SetCommand { get; }

        public OnlineStatusViewModel()
        {
            OpenCommand = new RelayCommand(() => IsOpen = !IsOpen);
            SetCommand = new AsyncRelayCommand<string?>(SetAsync);
        }

        private async Task LoadAsync()
        {
            if (_busy)
                return;

            if (!await SignedInAsync())
            {
                _online = null;
                Show(Strings.Menu_Overlay_Privacy_NeedsCookies);
                Refreshed();
                return;
            }

            _busy = true;
            Show(Strings.Menu_Overlay_Privacy_Loading);

            try
            {
                (_online, _join) = await PrivacySettings.FetchAsync();

                App.Logger.Info($"Online visibility is {_online ?? "unreported"}, joining is {_join ?? "unreported"}");

                Show(null);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to read the privacy settings");
                App.Logger.Error(ex);

                _online = null;
                Show(Strings.Menu_Overlay_Privacy_LoadFailed);
            }
            finally
            {
                _busy = false;

                Refreshed();
            }
        }

        private async Task SetAsync(string? value)
        {
            if (value is null || !CanChange || value == _online)
                return;

            _busy = true;
            Show(Strings.Menu_Overlay_Privacy_Saving);
            Refreshed();

            string message;

            try
            {
                bool narrowed = await PrivacySettings.SetOnlineVisibilityAsync(value, _join);

                App.Logger.Info($"Online visibility is now {value}{(narrowed ? ", with joining narrowed to match" : "")}");

                message = narrowed
                    ? Strings.Menu_Overlay_Privacy_JoinNarrowed
                    : Strings.Menu_Overlay_Privacy_Saved;
            }
            catch (SettingRejectedException ex) when (!String.IsNullOrWhiteSpace(ex.Reason))
            {
                App.Logger.Warn($"Roblox refused the privacy change: {ex.Message}");

                message = String.Format(
                    CultureInfo.InvariantCulture,
                    Strings.Menu_Overlay_Privacy_Rejected,
                    ex.Reason);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to set online visibility to {value}");
                App.Logger.Error(ex);

                message = Strings.Menu_Overlay_Privacy_SaveFailed;
            }

            try
            {
                (_online, _join) = await PrivacySettings.FetchAsync();
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to read the privacy settings back");
                App.Logger.Error(ex);
            }

            _busy = false;
            Show(message);
            Refreshed();
        }

        private static async Task<bool> SignedInAsync()
        {
            if (!App.Settings.Prop.AllowCookieAccess)
                return false;

            if (!App.Cookies.Loaded)
                await Task.Run(App.Cookies.LoadCookies);

            return App.Cookies.Loaded;
        }

        private static string LabelFor(string value) => value switch
        {
            "AllUsers" => Strings.Menu_Overlay_Privacy_Everyone,
            "FriendsFollowingAndFollowers" => Strings.Menu_Overlay_Privacy_FriendsFollowingAndFollowers,
            "FriendsAndFollowing" => Strings.Menu_Overlay_Privacy_FriendsAndFollowing,
            "Friends" => Strings.Menu_Overlay_Privacy_Friends,
            "TrustedFriends" => Strings.Menu_Overlay_Privacy_TrustedFriends,
            "NoOne" => Strings.Menu_Overlay_Privacy_NoOne,
            _ => value
        };

        private void Show(string? status)
        {
            _status = status;

            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(HasStatus));
        }

        private void Refreshed()
        {
            OnPropertyChanged(nameof(Options));
            OnPropertyChanged(nameof(CanChange));
        }
    }
}