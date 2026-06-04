using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Froststrap.RobloxInterfaces;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    public class ChannelViewModel : NotifyPropertyChangedViewModel
    {
        private CancellationTokenSource? _playerCts;
        private CancellationTokenSource? _studioCts;

        public ICommand OpenWineCfgCommand { get; }

        public ChannelViewModel()
        {
            _ = LoadChannelDeployInfo(App.Settings.Prop.PlayerChannel, false);
            _ = LoadChannelDeployInfo(App.Settings.Prop.StudioChannel, true);

            StudioEnvEntries = [];
            foreach (var kv in App.Settings.Prop.StudioEnvironmentVariables)
                StudioEnvEntries.Add(new EnvEntry(kv.Key, kv.Value, RemoveEnvEntry));

            AddStudioEnvCommand = new RelayCommand(() =>
            {
                var newEntry = new EnvEntry("", "", RemoveEnvEntry);
                StudioEnvEntries.Add(newEntry);
            }); 
            
            OpenWineCfgCommand = new RelayCommand(OpenWineCfg);

            OnPropertyChanged(nameof(IsWineAvailable));
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

        private void OpenWineCfg()
        {
            string wineBinary = App.Settings.Prop.WineBinaryPath;
            if (string.IsNullOrEmpty(wineBinary) || !File.Exists(wineBinary))
            {
                wineBinary = Path.Combine(Paths.Base, "kombucha", "bin", "wine");
                if (!File.Exists(wineBinary))
                {
                    _ = Frontend.ShowMessageBox("Wine binary not found. Please ensure Wine is installed.", MessageBoxImage.Error);
                    return;
                }
            }

            string winePrefix = App.Settings.Prop.WinePrefixPath ?? Path.Combine(Paths.Base, "prefixes", "studio");
            var psi = new ProcessStartInfo
            {
                FileName = wineBinary,
                Arguments = "winecfg",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.EnvironmentVariables["WINEPREFIX"] = winePrefix;
            psi.EnvironmentVariables["WINEDLLOVERRIDES"] = "winemenubuilder.exe=d";

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _ = Frontend.ShowMessageBox($"Failed to start winecfg: {ex.Message}", MessageBoxImage.Error);
            }
        }

        private static string GetEffectiveWineBinary()
        {
            string? customPath = App.Settings.Prop.WineBinaryPath;
            if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
                return customPath;

            string kombuchaPath = Path.Combine(Paths.Base, "kombucha", "bin", "wine");
            return File.Exists(kombuchaPath) ? kombuchaPath : "wine";
        }

        public static bool IsWineAvailable
        {
            get
            {
                if (!OperatingSystem.IsLinux()) return false;
                string wineBinary = GetEffectiveWineBinary();
                if (!File.Exists(wineBinary)) return false;

                string winePrefix = App.Settings.Prop.WinePrefixPath ?? Path.Combine(Paths.Base, "prefixes", "studio");
                return Directory.Exists(winePrefix);
            }
        }

        private static Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
            return null;
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
                    SelectedUpdateCheck = UpdateCheck.Both;
                else if (SelectedUpdateCheck is UpdateCheck.Test or UpdateCheck.Both)
                    SelectedUpdateCheck = UpdateCheck.Stable;
                else
                    OnPropertyChanged(nameof(PreReleaseUpdatesEnabled));
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
                    _ = HandleTestModeConfirmation();
                else
                {
                    App.LaunchSettings.TestModeFlag.Active = value;
                    OnPropertyChanged(nameof(TestModeEnabled));
                }
            }
        }

        public static bool GameSearch
        {
            get => App.Settings.Prop.GameSearch;
            set => App.Settings.Prop.GameSearch = value;
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

        private static string NormalizeChannel(string channel)
        {
            if (string.Equals(channel, "live", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(channel, "zlive", StringComparison.OrdinalIgnoreCase))
                return Deployment.DefaultChannel;
            return channel;
        }

        private async Task LoadChannelDeployInfo(string channel, bool isStudio)
        {
            var cts = new CancellationTokenSource();
            if (isStudio)
            {
                _studioCts?.Cancel();
                _studioCts = cts;
            }
            else
            {
                _playerCts?.Cancel();
                _playerCts = cts;
            }

            var token = cts.Token;

            try
            {
                if (isStudio)
                {
                    StudioShowLoadingError = false;
                    StudioInfoLoadingText = Strings.Menu_Channel_Switcher_Fetching;
                    StudioDeployInfo = null;
                    OnPropertyChanged(nameof(StudioShowLoadingError));
                    OnPropertyChanged(nameof(StudioInfoLoadingText));
                    OnPropertyChanged(nameof(StudioDeployInfo));
                }
                else
                {
                    PlayerShowLoadingError = false;
                    PlayerInfoLoadingText = Strings.Menu_Channel_Switcher_Fetching;
                    PlayerDeployInfo = null;
                    OnPropertyChanged(nameof(PlayerShowLoadingError));
                    OnPropertyChanged(nameof(PlayerInfoLoadingText));
                    OnPropertyChanged(nameof(PlayerDeployInfo));
                }

                if (token.IsCancellationRequested) return;

                string binaryType = isStudio
                    ? (OperatingSystem.IsMacOS() ? "MacStudio" : "WindowsStudio64")
                    : (OperatingSystem.IsMacOS() ? "MacPlayer" : "WindowsPlayer");

                bool isPrivate = await Deployment.IsChannelPrivate(channel);
                if (token.IsCancellationRequested) return;

                if (App.Cookies.Loaded && isPrivate && string.IsNullOrEmpty(Deployment.ChannelToken))
                {
                    UserChannel? userChannel = await Deployment.GetUserChannel(binaryType);
                    if (userChannel?.Token is not null)
                        Deployment.ChannelToken = userChannel.Token;
                }

                ClientVersion info = await Deployment.GetInfo(channel, true, true, binaryType);
                if (token.IsCancellationRequested) return;

                var deployInfo = new DeployInfo
                {
                    Version = info.Version,
                    VersionGuid = isPrivate ? "version-private" : info.VersionGuid,
                    Timestamp = info.Timestamp?.ToLocalTime().ToString() ?? "?"
                };

                if (isStudio)
                {
                    StudioDeployInfo = deployInfo;
                    StudioShowChannelWarning = info.IsBehindDefaultChannel;
                    OnPropertyChanged(nameof(StudioDeployInfo));
                    OnPropertyChanged(nameof(StudioShowChannelWarning));
                }
                else
                {
                    PlayerDeployInfo = deployInfo;
                    PlayerShowChannelWarning = info.IsBehindDefaultChannel;
                    OnPropertyChanged(nameof(PlayerDeployInfo));
                    OnPropertyChanged(nameof(PlayerShowChannelWarning));
                }
            }
            catch (OperationCanceledException) { }
            catch (InvalidChannelException ex)
            {
                if (token.IsCancellationRequested) return;

                string errorText;
                if (ex.StatusCode.HasValue && ex.StatusCode.Value == HttpStatusCode.Unauthorized)
                    errorText = Strings.Menu_Channel_Switcher_Unauthorized;
                else if (ex.StatusCode.HasValue)
                    errorText = $"HTTP error {(int)ex.StatusCode.Value}";
                else
                    errorText = "An unknown HTTP error occurred.";

                if (isStudio)
                {
                    StudioShowLoadingError = true;
                    StudioInfoLoadingText = errorText;
                    OnPropertyChanged(nameof(StudioShowLoadingError));
                    OnPropertyChanged(nameof(StudioInfoLoadingText));
                }
                else
                {
                    PlayerShowLoadingError = true;
                    PlayerInfoLoadingText = errorText;
                    OnPropertyChanged(nameof(PlayerShowLoadingError));
                    OnPropertyChanged(nameof(PlayerInfoLoadingText));
                }
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested) return;
                if (isStudio)
                {
                    StudioShowLoadingError = true;
                    StudioInfoLoadingText = "Failed to load channel data.";
                    OnPropertyChanged(nameof(StudioShowLoadingError));
                    OnPropertyChanged(nameof(StudioInfoLoadingText));
                }
                else
                {
                    PlayerShowLoadingError = true;
                    PlayerInfoLoadingText = "Failed to load channel data.";
                    OnPropertyChanged(nameof(PlayerShowLoadingError));
                    OnPropertyChanged(nameof(PlayerInfoLoadingText));
                }
                App.Logger.WriteException("ChannelViewModel::LoadChannelDeployInfo", ex);
            }
        }

        public DeployInfo? PlayerDeployInfo { get; private set; }
        public DeployInfo? StudioDeployInfo { get; private set; }

        public bool PlayerShowLoadingError { get; set; }
        public bool StudioShowLoadingError { get; set; }
        public string PlayerInfoLoadingText { get; private set; } = "";
        public string StudioInfoLoadingText { get; private set; } = "";
        public bool PlayerShowChannelWarning { get; set; }
        public bool StudioShowChannelWarning { get; set; }

        public string PlayerChannel
        {
            get => App.Settings.Prop.PlayerChannel;
            set
            {
                value = value.Trim();
                App.Settings.Prop.PlayerChannel = NormalizeChannel(value);
                OnPropertyChanged();
                _ = LoadChannelDeployInfo(value, false);
            }
        }

        public string StudioChannel
        {
            get => App.Settings.Prop.StudioChannel;
            set
            {
                value = value.Trim();
                App.Settings.Prop.StudioChannel = NormalizeChannel(value);
                OnPropertyChanged();
                _ = LoadChannelDeployInfo(value, true);
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

        public IEnumerable<StudioRenderer> StudioRendererOptions { get; } = Enum.GetValues<StudioRenderer>();

        public static StudioRenderer SelectedStudioRenderer
        {
            get => App.Settings.Prop.StudioRenderer;
            set => App.Settings.Prop.StudioRenderer = value;
        }

        public static bool StudioGameMode
        {
            get => App.Settings.Prop.StudioGameMode;
            set => App.Settings.Prop.StudioGameMode = value;
        }

        public static bool StudioDebug
        {
            get => App.Settings.Prop.StudioDebug;
            set => App.Settings.Prop.StudioDebug = value;
        }

        public bool VirtualDesktopEnabled
        {
            get => !string.IsNullOrEmpty(App.Settings.Prop.StudioVirtualDesktop);
            set
            {
                if (!value)
                    VirtualDesktopResolution = "";
                else if (string.IsNullOrEmpty(VirtualDesktopResolution))
                    VirtualDesktopResolution = "1920x1080";
                OnPropertyChanged(nameof(VirtualDesktopEnabled));
                OnPropertyChanged(nameof(VirtualDesktopResolution));
            }
        }

        public static string VirtualDesktopResolution
        {
            get => App.Settings.Prop.StudioVirtualDesktop ?? string.Empty;
            set => App.Settings.Prop.StudioVirtualDesktop = value;
        }

        public static string StudioLauncher
        {
            get => App.Settings.Prop.StudioLauncher ?? string.Empty;
            set => App.Settings.Prop.StudioLauncher = value;
        }

        public static bool EnableWebView2
        {
            get => App.Settings.Prop.EnableWebView2;
            set => App.Settings.Prop.EnableWebView2 = value;
        }

        public ObservableCollection<EnvEntry> StudioEnvEntries { get; set; }
        public ICommand AddStudioEnvCommand { get; }

        public class EnvEntry : NotifyPropertyChangedViewModel
        {
            private string _key;
            private string _value;
            private string _originalKey;
            private readonly Action<EnvEntry> _removeAction;

            public string Key
            {
                get => _key;
                set
                {
                    if (_key == value) return;
                    var oldKey = _key;
                    _key = value;
                    OnPropertyChanged();
                    UpdateDictionary(oldKey);
                }
            }

            public string Value
            {
                get => _value;
                set
                {
                    if (_value == value) return;
                    _value = value;
                    OnPropertyChanged();
                    UpdateDictionary(_key);
                }
            }

            public ICommand RemoveCommand { get; }

            public EnvEntry(string key, string value, Action<EnvEntry> removeAction)
            {
                _key = key;
                _originalKey = key;
                _value = value;
                _removeAction = removeAction;
                RemoveCommand = new RelayCommand(() => _removeAction(this));
            }

            private void UpdateDictionary(string currentKey)
            {
                var dict = App.Settings.Prop.StudioEnvironmentVariables;

                if (!string.IsNullOrEmpty(_originalKey) && _originalKey != currentKey)
                    dict.Remove(_originalKey);

                if (!string.IsNullOrWhiteSpace(currentKey))
                    dict[currentKey] = Value;
                else
                    dict.Remove(currentKey);

                _originalKey = currentKey;
                App.Settings.Save();
            }
        }

        private void RemoveEnvEntry(EnvEntry entry)
        {
            App.Settings.Prop.StudioEnvironmentVariables.Remove(entry.Key);
            StudioEnvEntries.Remove(entry);
        }

        private async Task ImportSettingsAsync(object? parameter)
        {
            var topLevel = parameter as Control != null ? TopLevel.GetTopLevel(parameter as Control) : GetMainWindow();
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Strings.Menu_BottomButtons_ImportSettings,
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });

            if (files.Count == 0) return;

            string? sourcePath = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return;

            try
            {
                string? dir = Path.GetDirectoryName(App.Settings.FileLocation);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
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
            var topLevel = parameter as Control != null ? TopLevel.GetTopLevel(parameter as Control) : GetMainWindow();
            if (topLevel == null) return;

            App.Settings.Save();

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Strings.Menu_BottomButtons_ExportSettings,
                SuggestedFileName = "Settings.json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });

            if (file is null) return;

            string? destinationPath = file.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(destinationPath)) return;

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
            OnPropertyChanged(nameof(IsRobloxInstallationMissing));
            OnPropertyChanged(nameof(StudioVersionOverrideEnabled));
            OnPropertyChanged(nameof(StudioVersionOverrideHash));
            OnPropertyChanged(nameof(PlayerChannel));
            OnPropertyChanged(nameof(StudioChannel));
            SetHashValidationState(StudioHashValidationState.Idle, string.Empty);
            OnPropertyChanged(nameof(SelectedStudioRenderer));
            OnPropertyChanged(nameof(StudioGameMode));
            OnPropertyChanged(nameof(StudioDebug));
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

            if (token.IsCancellationRequested) return;

            try
            {
                string baseUrl = string.IsNullOrEmpty(Deployment.BaseUrl)
                    ? "https://setup.rbxcdn.com"
                    : Deployment.BaseUrl;

                string manifestUrl = $"{baseUrl}/{hash}-rbxPkgManifest.txt";

                using var request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
                using var response = await App.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

                if (token.IsCancellationRequested) return;

                if (response.IsSuccessStatusCode)
                    SetHashValidationState(StudioHashValidationState.Valid, "Version found.");
                else if (response.StatusCode == HttpStatusCode.NotFound)
                    SetHashValidationState(StudioHashValidationState.Invalid, "Version not found on Roblox servers.");
                else
                    SetHashValidationState(StudioHashValidationState.Invalid, $"Unexpected response: {(int)response.StatusCode} {response.StatusCode}");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    SetHashValidationState(StudioHashValidationState.Invalid, $"Network error: {ex.Message}");
            }
        }
    }
}