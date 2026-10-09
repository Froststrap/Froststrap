using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Froststrap.Enums;
using Froststrap.Models.Entities;
using Froststrap.RobloxInterfaces;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    internal class VisibilityOption
    {
        public PrivacyLevel Value { get; init; }

        public string Label { get; init; } = String.Empty;

        public bool IsCurrent { get; init; }

        public bool IsAllowed { get; init; } = true;
    }

    internal abstract class VisibilityViewModel : NotifyPropertyChangedViewModel
    {
        private bool _busy;
        private string? _status;
        private bool _isOpen;

        protected PrivacyState? State { get; private set; }

        public abstract string Title { get; }

        public abstract string Hint { get; }

        protected abstract string SettingName { get; }

        protected abstract IReadOnlyList<PrivacyLevel> Available { get; }

        protected abstract PrivacyLevel? Current { get; }

        protected virtual bool IsAllowed(PrivacyLevel value) => true;

        protected abstract Task<string> ApplyAsync(PrivacyLevel value);

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

        public IReadOnlyList<VisibilityOption> Options => [.. Enum.GetValues<PrivacyLevel>()
            .Where(x => x == Current || Available.Count == 0 || Available.Contains(x))
            .Select(x => new VisibilityOption
            {
                Value = x,
                Label = LabelFor(x),
                IsCurrent = x == Current,
                IsAllowed = IsAllowed(x)
            })];

        public bool CanChange => !_busy && Current is not null;

        public string StatusText => _status ?? String.Empty;

        public bool HasStatus => _status is not null;

        public ICommand OpenCommand { get; }
        public ICommand SetCommand { get; }

        protected VisibilityViewModel()
        {
            OpenCommand = new RelayCommand(() => IsOpen = !IsOpen);
            SetCommand = new AsyncRelayCommand<PrivacyLevel>(SetAsync);

            App.Cookies.WatchAccount(this, static vm => vm.OnAccountChanged());
        }

        private void OnAccountChanged() => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            State = null;

            Show(null);
            Refreshed();

            if (_isOpen)
                _ = LoadAsync();
        });

        private async Task LoadAsync()
        {
            string logIdent = $"{GetType().Name}::LoadAsync";

            if (_busy)
                return;

            if (!await App.Cookies.EnsureLoadedAsync())
            {
                State = null;
                Show(Strings.Menu_Overlay_Privacy_NeedsCookies);
                Refreshed();
                return;
            }

            _busy = true;
            Show(Strings.Menu_Overlay_Privacy_Loading);
            Refreshed();

            try
            {
                State = await PrivacySettings.FetchAsync();

                App.Logger.Info($"{logIdent}: Online visibility is {State.Online?.ToString() ?? "unreported"}, joining is {State.Join?.ToString() ?? "unreported"}");

                Show(null);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"{logIdent}: Failed to read the privacy settings");
                App.Logger.Error(ex);

                State = null;
                Show(Strings.Menu_Overlay_Privacy_LoadFailed);
            }
            finally
            {
                _busy = false;
                Refreshed();
            }
        }

        private async Task SetAsync(PrivacyLevel value)
        {
            string logIdent = $"{GetType().Name}::SetAsync";

            if (!CanChange || value == Current || !IsAllowed(value))
                return;

            _busy = true;
            Show(Strings.Menu_Overlay_Privacy_Saving);
            Refreshed();

            string message;

            try
            {
                message = await ApplyAsync(value);
            }
            catch (SettingRejectedException ex) when (!String.IsNullOrWhiteSpace(ex.Reason))
            {
                App.Logger.Warn($"{logIdent}: Roblox refused it: {ex.Message}");

                message = String.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    Strings.Menu_Overlay_Privacy_Rejected,
                    ex.Reason);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"{logIdent}: Failed to set {SettingName} to {value}");
                App.Logger.Error(ex);

                message = Strings.Menu_Overlay_Privacy_SaveFailed;
            }

            try
            {
                State = await PrivacySettings.FetchAsync();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"{logIdent}: Failed to read the privacy settings back");
                App.Logger.Error(ex);
            }

            _busy = false;
            Show(message);
            Refreshed();
        }

        private static string LabelFor(PrivacyLevel level) => level switch
        {
            PrivacyLevel.Everyone => Strings.Menu_Overlay_Privacy_Everyone,
            PrivacyLevel.FriendsFollowingAndFollowers => Strings.Menu_Overlay_Privacy_FriendsFollowingAndFollowers,
            PrivacyLevel.FriendsAndFollowing => Strings.Menu_Overlay_Privacy_FriendsAndFollowing,
            PrivacyLevel.Friends => Strings.Menu_Overlay_Privacy_Friends,
            PrivacyLevel.TrustedFriends => Strings.Menu_Overlay_Privacy_TrustedFriends,
            _ => Strings.Menu_Overlay_Privacy_NoOne
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