using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Froststrap.RobloxInterfaces;
using System.Net;
using System.Text.Json;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    public class ChannelViewModel : NotifyPropertyChangedViewModel
    {
        private CancellationTokenSource? _cts;

        public ChannelViewModel()
        {
            Task.Run(() => LoadChannelDeployInfo(App.Settings.Prop.Channel));
        }

        public static IEnumerable<UpdateCheck> UpdateCheckValues => Enum.GetValues<UpdateCheck>();

        public bool AutomaticUpdatesEnabled
        {
            get => SelectedUpdateCheck != UpdateCheck.Disabled;
            set
            {
                if (value)
                {
                    if (SelectedUpdateCheck == UpdateCheck.Disabled)
                        SelectedUpdateCheck = UpdateCheck.Stable;
                    else
                        OnPropertyChanged(nameof(AutomaticUpdatesEnabled));
                }
                else if (SelectedUpdateCheck != UpdateCheck.Disabled)
                {
                    SelectedUpdateCheck = UpdateCheck.Disabled;
                }

                OnPropertyChanged(nameof(PreReleaseUpdatesEnabled));
            }
        }

        public ICommand ImportSettingsCommand => new AsyncRelayCommand<object?>(ImportSettingsAsync);

        public ICommand ExportSettingsCommand => new AsyncRelayCommand<object?>(ExportSettingsAsync);

        public ICommand ResetSettingsToDefaultCommand => new RelayCommand(ResetSettingsToDefault);

        public bool PreReleaseUpdatesEnabled
        {
            get => SelectedUpdateCheck is UpdateCheck.Test or UpdateCheck.Both;
            set
            {
                if (value)
                {
                    SelectedUpdateCheck = UpdateCheck.Both;
                }
                else if (SelectedUpdateCheck is UpdateCheck.Test or UpdateCheck.Both)
                {
                    SelectedUpdateCheck = UpdateCheck.Stable;
                }
                else
                {
                    OnPropertyChanged(nameof(PreReleaseUpdatesEnabled));
                }
            }
        }

        private static bool ValidateDomain(string domain)
        {
            const string domainPattern = @"^([a-zA-Z0-9.-]+)\.([a-zA-Z0-9]+)$";

            return Regex.IsMatch(domain, domainPattern);
        }

        public static string RobloxDomain
        {
            get => App.Settings.Prop.RobloxDomain;
            set
            {
                if (ValidateDomain(value))
                    App.Settings.Prop.RobloxDomain = value;
                else
                    _ = Frontend.ShowMessageBox("You entered an invalid domain\nPlease dont mess with this if you dont know what your doing", MessageBoxImage.Warning, MessageBoxButton.OK);
            }
        }

        public bool TestModeEnabled
        {
            get => App.LaunchSettings.TestModeFlag.Active;
            set
            {
                if (value && !App.State.Prop.TestModeWarningShown)
                {
                    _ = HandleTestModeConfirmation();
                }
                else
                {
                    App.LaunchSettings.TestModeFlag.Active = value;
                    OnPropertyChanged(nameof(TestModeEnabled));
                }
            }
        }

        private async Task HandleTestModeConfirmation()
        {
            var result = await Frontend.ShowMessageBox(Strings.Menu_TestMode_Prompt, MessageBoxImage.Information, MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                App.State.Prop.TestModeWarningShown = true;
                App.LaunchSettings.TestModeFlag.Active = true;
            }

            OnPropertyChanged(nameof(TestModeEnabled));
        }

        public UpdateCheck SelectedUpdateCheck
        {
            get => App.Settings.Prop.UpdateChecks;
            set
            {
                App.Settings.Prop.UpdateChecks = value;
                OnPropertyChanged(nameof(SelectedUpdateCheck));
                OnPropertyChanged(nameof(AutomaticUpdatesEnabled));
                OnPropertyChanged(nameof(PreReleaseUpdatesEnabled));
            }
        }

        public static bool IsRobloxInstallationMissing
        {
            get
            {
                if (OperatingSystem.IsLinux())
                {
                    var clientPath = Path.Combine(Paths.Versions, "Sober", "data", "sober", "packages", "x86_64", "com.roblox.client");
                    bool isLinuxPlayerInstalled = Directory.Exists(clientPath) && Directory.EnumerateFiles(clientPath, "*", SearchOption.AllDirectories).Any();

                    return !isLinuxPlayerInstalled;
                }

                return !App.IsPlayerInstalled && !App.IsStudioInstalled;
            }
        }


        private async Task LoadChannelDeployInfo(string channel)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                ShowLoadingError = false;
                OnPropertyChanged(nameof(ShowLoadingError));

                ChannelInfoLoadingText = Strings.Menu_Channel_Switcher_Fetching;
                OnPropertyChanged(nameof(ChannelInfoLoadingText));

                ChannelDeployInfo = null;
                OnPropertyChanged(nameof(ChannelDeployInfo));

                if (token.IsCancellationRequested) return;

                bool isPrivate = await Deployment.IsChannelPrivate(channel);

                if (token.IsCancellationRequested) return;

                if (App.Cookies.Loaded && isPrivate && string.IsNullOrEmpty(Deployment.ChannelToken))
                {
                    UserChannel? userChannel = await Deployment.GetUserChannel(OperatingSystem.IsMacOS() ? "MacPlayer" : "WindowsPlayer");
                    if (userChannel?.Token is not null)
                        Deployment.ChannelToken = userChannel.Token;
                }

                ClientVersion info = await Deployment.GetInfo(channel, true, true);

                if (token.IsCancellationRequested) return;

                ShowChannelWarning = info.IsBehindDefaultChannel;
                OnPropertyChanged(nameof(ShowChannelWarning));

                ChannelDeployInfo = new DeployInfo
                {
                    Version = info.Version,
                    VersionGuid = isPrivate ? "version-private" : info.VersionGuid,
                    Timestamp = info.Timestamp?.ToLocalTime().ToString() ?? "?"
                };

                App.State.Prop.IgnoreOutdatedChannel = true;
                OnPropertyChanged(nameof(ChannelDeployInfo));
            }
            catch (OperationCanceledException) { /* Do nothing, task was replaced */ }
            catch (InvalidChannelException ex)
            {
                if (token.IsCancellationRequested) return;

                ShowLoadingError = true;
                OnPropertyChanged(nameof(ShowLoadingError));

                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                    ChannelInfoLoadingText = Strings.Menu_Channel_Switcher_Unauthorized;
                else
                    ChannelInfoLoadingText = $"An http error has occured ({ex.StatusCode})";

                OnPropertyChanged(nameof(ChannelInfoLoadingText));
            }
        }

        public bool ShowLoadingError { get; set; } = false;
        public bool ShowChannelWarning { get; set; } = false;

        public DeployInfo? ChannelDeployInfo { get; private set; } = null;
        public string ChannelInfoLoadingText { get; private set; } = null!;

        public string ViewChannel
        {
            get => App.Settings.Prop.Channel;
            set
            {
                value = value.Trim();

                _ = LoadChannelDeployInfo(value);

                if (value.Equals("live", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("zlive", StringComparison.OrdinalIgnoreCase))
                {
                    App.Settings.Prop.Channel = Deployment.DefaultChannel;
                }
                else
                {
                    App.Settings.Prop.Channel = value;
                }

                OnPropertyChanged(nameof(ViewChannel));
            }
        }

        public static bool UpdateRoblox
        {
            get => App.Settings.Prop.UpdateRoblox && !IsRobloxInstallationMissing;
            set => App.Settings.Prop.UpdateRoblox = value;
        }

        public static bool AutomaticallyUpdateSober
        {
            get => App.Settings.Prop.AutomaticallyUpdateSober;
            set => App.Settings.Prop.AutomaticallyUpdateSober = value;
        }

        public static int MaxThreadDownload
        {
            get => App.Settings.Prop.MaxThreadDownload;
            set => App.Settings.Prop.MaxThreadDownload = value;
        }

        public static bool StaticDirectory
        {
            get => App.Settings.Prop.StaticDirectory;
            set => App.Settings.Prop.StaticDirectory = value;
        }

        private async Task ImportSettingsAsync(object? parameter)
        {
            var topLevel = TopLevel.GetTopLevel(parameter as Control);

            if (topLevel == null)
                return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Strings.Menu_BottomButtons_ImportSettings,
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });

            if (files.Count == 0)
                return;

            string? sourcePath = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(App.Settings.FileLocation)!);
                File.Copy(sourcePath, App.Settings.FileLocation, true);
                App.Settings.Load();
                RefreshBindings();
            }
            catch (Exception ex)
            {
                await Frontend.ShowMessageBox(ex.Message, MessageBoxImage.Error);
            }
        }

        private async Task ExportSettingsAsync(object? parameter)
        {
            var topLevel = TopLevel.GetTopLevel(parameter as Control);

            if (topLevel == null)
                return;

            App.Settings.Save();

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Strings.Menu_BottomButtons_ExportSettings,
                SuggestedFileName = "Settings.json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });

            if (file is null)
                return;

            string? destinationPath = file.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(destinationPath))
                return;

            try
            {
                File.Copy(App.Settings.FileLocation, destinationPath, true);
            }
            catch (Exception ex)
            {
                await Frontend.ShowMessageBox(ex.Message, MessageBoxImage.Error);
            }
        }

        private void ResetSettingsToDefault()
        {
            App.Settings.Prop = new global::Froststrap.Models.Persistable.Settings();
            App.Settings.Save();
            RefreshBindings();
        }

        private void RefreshBindings()
        {
            OnPropertyChanged(nameof(AutomaticUpdatesEnabled));
            OnPropertyChanged(nameof(PreReleaseUpdatesEnabled));
            OnPropertyChanged(nameof(SelectedUpdateCheck));
            OnPropertyChanged(nameof(ForceRobloxReinstallation));
            OnPropertyChanged(nameof(UpdateRoblox));
            OnPropertyChanged(nameof(AutomaticallyUpdateSober));
            OnPropertyChanged(nameof(StaticDirectory));
            OnPropertyChanged(nameof(TestModeEnabled));
            OnPropertyChanged(nameof(ViewChannel));
            OnPropertyChanged(nameof(IsRobloxInstallationMissing));
            OnPropertyChanged(nameof(StudioVersionOverrideEnabled));
            OnPropertyChanged(nameof(StudioVersionOverrideHash));
            SetHashValidationState(StudioHashValidationState.Idle, string.Empty);
        }

        public static IReadOnlyDictionary<string, ChannelChangeMode> ChannelChangeModes => new Dictionary<string, ChannelChangeMode>
        {
            { Strings.Menu_Channel_ChangeAction_Automatic, ChannelChangeMode.Automatic },
            { Strings.Menu_Channel_ChangeAction_Prompt, ChannelChangeMode.Prompt },
            { Strings.Menu_Channel_ChangeAction_Ignore, ChannelChangeMode.Ignore },
        };

        public static string SelectedChannelChangeMode
        {
            get => ChannelChangeModes.FirstOrDefault(x => x.Value == App.Settings.Prop.ChannelChangeMode).Key;
            set => App.Settings.Prop.ChannelChangeMode = ChannelChangeModes[value];
        }

        public static bool ForceRobloxReinstallation
        {
            get => App.State.Prop.ForceReinstall || IsRobloxInstallationMissing;
            set => App.State.Prop.ForceReinstall = value;
        }

        public bool StudioVersionOverrideEnabled
        {
            get => App.Settings.Prop.StudioVersionOverrideEnabled;
            set
            {
                App.Settings.Prop.StudioVersionOverrideEnabled = value;
                OnPropertyChanged(nameof(StudioVersionOverrideEnabled));

                if (value && !string.IsNullOrWhiteSpace(App.Settings.Prop.StudioVersionOverrideHash))
                    _ = ValidateStudioVersionHashAsync(App.Settings.Prop.StudioVersionOverrideHash);
                else if (!value)
                    SetHashValidationState(StudioHashValidationState.Idle, string.Empty);
            }
        }

        public string StudioVersionOverrideHash
        {
            get => App.Settings.Prop.StudioVersionOverrideHash;
            set
            {
                value = value?.Trim() ?? string.Empty;
                App.Settings.Prop.StudioVersionOverrideHash = value;
                OnPropertyChanged(nameof(StudioVersionOverrideHash));

                if (App.Settings.Prop.StudioVersionOverrideEnabled)
                    _ = ValidateStudioVersionHashAsync(value);
                else
                    SetHashValidationState(StudioHashValidationState.Idle, string.Empty);
            }
        }

        public enum StudioHashValidationState { Idle, Checking, Valid, Invalid }

        private StudioHashValidationState _hashValidationState = StudioHashValidationState.Idle;
        private string _hashValidationMessage = string.Empty;
        private CancellationTokenSource? _hashValidationCts;

        public StudioHashValidationState HashValidationState
        {
            get => _hashValidationState;
            private set
            {
                _hashValidationState = value;
                OnPropertyChanged(nameof(HashValidationState));
                OnPropertyChanged(nameof(IsHashIdle));
                OnPropertyChanged(nameof(IsHashChecking));
                OnPropertyChanged(nameof(IsHashValid));
                OnPropertyChanged(nameof(IsHashInvalid));
            }
        }

        public string HashValidationMessage
        {
            get => _hashValidationMessage;
            private set
            {
                _hashValidationMessage = value;
                OnPropertyChanged(nameof(HashValidationMessage));
            }
        }

        public bool IsHashIdle => HashValidationState == StudioHashValidationState.Idle;
        public bool IsHashChecking => HashValidationState == StudioHashValidationState.Checking;
        public bool IsHashValid => HashValidationState == StudioHashValidationState.Valid;
        public bool IsHashInvalid => HashValidationState == StudioHashValidationState.Invalid;

        private void SetHashValidationState(StudioHashValidationState state, string message)
        {
            HashValidationState = state;
            HashValidationMessage = message;
        }

        private async Task ValidateStudioVersionHashAsync(string hash)
        {
            _hashValidationCts?.Cancel();
            _hashValidationCts = new CancellationTokenSource();
            var token = _hashValidationCts.Token;

            if (string.IsNullOrWhiteSpace(hash))
            {
                SetHashValidationState(StudioHashValidationState.Idle, string.Empty);
                return;
            }

            if (!Regex.IsMatch(hash, @"^version-[0-9a-f]{16}$", RegexOptions.IgnoreCase))
            {
                SetHashValidationState(StudioHashValidationState.Invalid, "Invalid format. Expected: version-xxxxxxxxxxxxxxxx");
                return;
            }

            SetHashValidationState(StudioHashValidationState.Checking, "Checking version...");

            try { await Task.Delay(500, token); }
            catch (OperationCanceledException) { return; }

            if (token.IsCancellationRequested)
                return;

            try
            {
                string baseUrl = string.IsNullOrEmpty(Deployment.BaseUrl)
                    ? "https://setup.rbxcdn.com"
                    : Deployment.BaseUrl;

                string manifestUrl = $"{baseUrl}/{hash}-rbxPkgManifest.txt";

                using var request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
                using var response = await App.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

                if (token.IsCancellationRequested)
                    return;

                if (response.IsSuccessStatusCode)
                {
                    SetHashValidationState(StudioHashValidationState.Valid, "Version found.");
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    SetHashValidationState(StudioHashValidationState.Invalid, "Version not found on Roblox servers.");
                }
                else
                {
                    SetHashValidationState(StudioHashValidationState.Invalid, $"Unexpected response: {(int)response.StatusCode} {response.StatusCode}");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    SetHashValidationState(StudioHashValidationState.Invalid, $"Network error: {ex.Message}");
            }
        }
    }
}