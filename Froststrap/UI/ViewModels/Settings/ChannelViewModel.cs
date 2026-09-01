using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Froststrap.RobloxInterfaces;
using Microsoft.Win32;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    internal class ChannelViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private CancellationTokenSource? _playerCts;
        private CancellationTokenSource? _studioCts;
        private CancellationTokenSource? _studioHashCts;
        private CancellationTokenSource? _playerHashCts;
        private bool _disposed;

        private bool _isMoving;

        public ChannelViewModel()
        {
            _ = LoadChannelDeployInfo(App.Settings.Prop.PlayerChannel, false);
            _ = LoadChannelDeployInfo(App.Settings.Prop.StudioChannel, true);
            BrowseInstallDirectoryCommand = new AsyncRelayCommand<object?>(BrowseInstallDirectoryAsync);
            MoveInstallDirectoryCommand = new AsyncRelayCommand<object?>(MoveInstallDirectoryAsync, CanMove);
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

        public string InstallDirectory
        {
            get
            {
                using var key = Registry.CurrentUser.OpenSubKey(App.UninstallKey);
                if (key?.GetValue("InstallLocation") is string location && Directory.Exists(location))
                    return location;

                return Paths.Base;
            }
            set
            {
                using var key = Registry.CurrentUser.CreateSubKey(App.UninstallKey);
                key?.SetValue("InstallLocation", value);

                OnPropertyChanged();
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
        public IAsyncRelayCommand BrowseInstallDirectoryCommand { get; }
        public IAsyncRelayCommand MoveInstallDirectoryCommand { get; }

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
                    _ = Frontend.ShowMessageBox(Strings.Menu_Deployment_DomainValidation, MessageBoxImage.Warning, MessageBoxButton.OK);
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
                await _studioCts!.CancelAsync();
                _studioCts = cts;
            }
            else
            {
                await _playerCts!.CancelAsync();
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
                    Timestamp = info.Timestamp?.ToLocalTime().ToString(CultureInfo.InvariantCulture) ?? "?"
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
                    StudioInfoLoadingText = Strings.Menu_Deployment_FailedToLoadChannelData;
                    OnPropertyChanged(nameof(StudioShowLoadingError));
                    OnPropertyChanged(nameof(StudioInfoLoadingText));
                }
                else
                {
                    PlayerShowLoadingError = true;
                    PlayerInfoLoadingText = Strings.Menu_Deployment_FailedToLoadChannelData;
                    OnPropertyChanged(nameof(PlayerShowLoadingError));
                    OnPropertyChanged(nameof(PlayerInfoLoadingText));
                }
                App.Logger.Error("Unhandled exception: ", ex);
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
            OnPropertyChanged(nameof(PlayerVersionOverrideEnabled));
            OnPropertyChanged(nameof(PlayerVersionOverrideHash));
            OnPropertyChanged(nameof(PlayerChannel));
            OnPropertyChanged(nameof(StudioChannel));

            SetStudioHashState(VersionHashValidationState.Idle, string.Empty);
            SetPlayerHashState(VersionHashValidationState.Idle, string.Empty);
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
                    SetStudioHashState(VersionHashValidationState.Idle, string.Empty);
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
                    SetStudioHashState(VersionHashValidationState.Idle, string.Empty);
            }
        }

        public bool PlayerVersionOverrideEnabled
        {
            get => App.Settings.Prop.PlayerVersionOverrideEnabled;
            set
            {
                if (App.Settings.Prop.PlayerVersionOverrideEnabled == value) return;
                App.Settings.Prop.PlayerVersionOverrideEnabled = value;
                OnPropertyChanged();
                if (value && !string.IsNullOrWhiteSpace(PlayerVersionOverrideHash))
                    _ = ValidatePlayerVersionHashAsync(PlayerVersionOverrideHash);
                else if (!value)
                    SetPlayerHashState(VersionHashValidationState.Idle, string.Empty);
            }
        }

        public string PlayerVersionOverrideHash
        {
            get => App.Settings.Prop.PlayerVersionOverrideHash;
            set
            {
                value = value?.Trim() ?? string.Empty;
                App.Settings.Prop.PlayerVersionOverrideHash = value;
                OnPropertyChanged();
                if (PlayerVersionOverrideEnabled)
                    _ = ValidatePlayerVersionHashAsync(value);
                else
                    SetPlayerHashState(VersionHashValidationState.Idle, string.Empty);
            }
        }

        internal enum VersionHashValidationState { Idle, Checking, Valid, Invalid }

        private VersionHashValidationState _studioHashState = VersionHashValidationState.Idle;
        private string _studioHashMessage = string.Empty;

        public VersionHashValidationState StudioHashValidationState
        {
            get => _studioHashState;
            private set
            {
                _studioHashState = value;
                OnPropertyChanged(nameof(StudioHashValidationState));
                OnPropertyChanged(nameof(IsStudioHashIdle));
                OnPropertyChanged(nameof(IsStudioHashChecking));
                OnPropertyChanged(nameof(IsStudioHashValid));
                OnPropertyChanged(nameof(IsStudioHashInvalid));
            }
        }

        public string StudioHashValidationMessage
        {
            get => _studioHashMessage;
            private set
            {
                _studioHashMessage = value;
                OnPropertyChanged(nameof(StudioHashValidationMessage));
            }
        }

        public bool IsStudioHashIdle => StudioHashValidationState == VersionHashValidationState.Idle;
        public bool IsStudioHashChecking => StudioHashValidationState == VersionHashValidationState.Checking;
        public bool IsStudioHashValid => StudioHashValidationState == VersionHashValidationState.Valid;
        public bool IsStudioHashInvalid => StudioHashValidationState == VersionHashValidationState.Invalid;

        private void SetStudioHashState(VersionHashValidationState state, string message)
        {
            StudioHashValidationState = state;
            StudioHashValidationMessage = message;
        }

        private VersionHashValidationState _playerHashState = VersionHashValidationState.Idle;
        private string _playerHashMessage = string.Empty;

        public VersionHashValidationState PlayerHashValidationState
        {
            get => _playerHashState;
            private set
            {
                _playerHashState = value;
                OnPropertyChanged(nameof(PlayerHashValidationState));
                OnPropertyChanged(nameof(IsPlayerHashIdle));
                OnPropertyChanged(nameof(IsPlayerHashChecking));
                OnPropertyChanged(nameof(IsPlayerHashValid));
                OnPropertyChanged(nameof(IsPlayerHashInvalid));
            }
        }

        public string PlayerHashValidationMessage
        {
            get => _playerHashMessage;
            private set
            {
                _playerHashMessage = value;
                OnPropertyChanged(nameof(PlayerHashValidationMessage));
            }
        }

        public bool IsPlayerHashIdle => PlayerHashValidationState == VersionHashValidationState.Idle;
        public bool IsPlayerHashChecking => PlayerHashValidationState == VersionHashValidationState.Checking;
        public bool IsPlayerHashValid => PlayerHashValidationState == VersionHashValidationState.Valid;
        public bool IsPlayerHashInvalid => PlayerHashValidationState == VersionHashValidationState.Invalid;

        private void SetPlayerHashState(VersionHashValidationState state, string message)
        {
            PlayerHashValidationState = state;
            PlayerHashValidationMessage = message;
        }

        public static async Task<(bool Valid, string Error)> ValidateHashCore(string hash, bool enforceAgeAndBan = false, string? binaryType = null)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return (false, "Hash is empty.");

            if (!Regex.IsMatch(hash, @"^version-[0-9a-f]{16}$", RegexOptions.IgnoreCase))
                return (false, "Invalid format. Expected: version-xxxxxxxxxxxxxxxx");

            try
            {
                string baseUrl = string.IsNullOrEmpty(Deployment.BaseUrl)
                    ? "https://setup.rbxcdn.com"
                    : Deployment.BaseUrl;

                string resourceUrl;

                if (OperatingSystem.IsMacOS())
                {
                    if (string.IsNullOrEmpty(binaryType))
                        return (false, "Binary type is required for macOS validation.");

                    string zipName = binaryType.Contains("Studio", StringComparison.OrdinalIgnoreCase)
                        ? "RobloxStudioApp.zip"
                        : "RobloxPlayer.zip";

                    resourceUrl = $"{baseUrl}/mac/{hash}-{zipName}";
                }
                else
                {
                    resourceUrl = $"{baseUrl}/{hash}-rbxPkgManifest.txt";
                }

                using var request = new HttpRequestMessage(HttpMethod.Head, resourceUrl);
                using var response = await App.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        return (false, "Version not found on Roblox servers.");
                    return (false, $"Unexpected response: {(int)response.StatusCode} {response.StatusCode}");
                }

                if (enforceAgeAndBan)
                {
                    await App.RemoteData.WaitUntilDataFetched();

                    if (App.RemoteData.Prop?.BannedVersionHashes?.Contains(hash) == true)
                        return (false, "This version is banned.");

                    DateTime? timestamp = await Deployment.GetVersionTimestamp(hash);
                    if (!timestamp.HasValue)
                        return (false, "Could not verify version date.");

                    if ((DateTime.UtcNow - timestamp.Value).TotalDays > 90)
                        return (false, "Version is older than 3 months.");
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Network error: {ex.Message}");
            }
        }

        private static async Task ValidateVersionHashAsync(string hash, Action<VersionHashValidationState, string> setState, CancellationToken token, bool enforceAgeAndBan = false, string? binaryType = null)
        {
            var (valid, error) = await ValidateHashCore(hash, enforceAgeAndBan, binaryType);

            if (token.IsCancellationRequested) return;

            if (valid)
                setState(VersionHashValidationState.Valid, "Version found.");
            else
                setState(VersionHashValidationState.Invalid, error);
        }

        private static string GetBinaryType(bool isStudio)
        {
            if (OperatingSystem.IsMacOS())
                return isStudio ? "MacStudio" : "MacPlayer";
            else
                return isStudio ? "WindowsStudio64" : "WindowsPlayer";
        }

        private async Task ValidateStudioVersionHashAsync(string hash)
        {
            await _studioHashCts!.CancelAsync();
            _studioHashCts = new CancellationTokenSource();
            var token = _studioHashCts.Token;

            await ValidateVersionHashAsync(
                hash,
                (state, msg) => SetStudioHashState(state, msg),
                token,
                enforceAgeAndBan: false,
                binaryType: GetBinaryType(true));
        }

        private async Task ValidatePlayerVersionHashAsync(string hash)
        {
            await _playerHashCts!.CancelAsync();
            _playerHashCts = new CancellationTokenSource();
            var token = _playerHashCts.Token;

            await ValidateVersionHashAsync(
                hash,
                (state, msg) => SetPlayerHashState(state, msg),
                token,
                enforceAgeAndBan: true,
                binaryType: GetBinaryType(false));
        }

        private async Task BrowseInstallDirectoryAsync(object? parameter)
        {
            var topLevel = GetTopLevel(parameter);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select New Installation Directory"
            });
            if (folders.Count > 0)
            {
                var path = folders[0].TryGetLocalPath();
                if (!string.IsNullOrEmpty(path))
                    InstallDirectory = path;
            }
        }

        private async Task MoveInstallDirectoryAsync(object? parameter)
        {
            if (_isMoving) return;

            string newDir = InstallDirectory;
            string currentDir = Paths.Base;

            if (string.Equals(newDir, currentDir, StringComparison.OrdinalIgnoreCase))
            {
                await Frontend.ShowMessageBox(Strings.Menu_Deployment_MoveInstallation_SameDirectory, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(newDir))
            {
                await Frontend.ShowMessageBox(Strings.Menu_Deployment_MoveInstallation_InvalidDirectory, MessageBoxImage.Warning);
                return;
            }

            var confirm = await Frontend.ShowMessageBox(
                string.Format(CultureInfo.InvariantCulture, Strings.Menu_Deployment_MoveInstallation_Confirm, currentDir, newDir),
                MessageBoxImage.Question,
                MessageBoxButton.YesNo);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                _isMoving = true;
                MoveInstallDirectoryCommand.NotifyCanExecuteChanged();

                App.Settings.Save();
                App.State.Save();
                App.FastFlags.Save();
                App.GlobalSettings.Save();
                App.AppStorage.Save();
                if (OperatingSystem.IsLinux())
                    App.SoberSettings.Save();

                await Task.Run(() => Installer.MoveInstallation(newDir));

                InstallDirectory = newDir;

                await Frontend.ShowMessageBox(Strings.Menu_Deployment_MoveInstallation_Success, MessageBoxImage.Information);

                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    UseShellExecute = true
                });
                App.Terminate();
            }
            catch (Exception ex)
            {
                await Frontend.ShowMessageBox(string.Format(CultureInfo.InvariantCulture, Strings.Menu_Deployment_MoveInstallation_Failed, ex.Message), MessageBoxImage.Error);
                App.Logger.Error("Unhandled exception: ", ex);
            }
            finally
            {
                _isMoving = false;
                MoveInstallDirectoryCommand.NotifyCanExecuteChanged();
            }
        }

        private bool CanMove(object? parameter) => !_isMoving;

        private static TopLevel? GetTopLevel(object? parameter)
        {
            if (parameter is Control control)
                return TopLevel.GetTopLevel(control);
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
            return null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _playerCts?.Cancel();
                _playerCts?.Dispose();
                _playerCts = null;

                _studioCts?.Cancel();
                _studioCts?.Dispose();
                _studioCts = null;

                _studioHashCts?.Cancel();
                _studioHashCts?.Dispose();
                _studioHashCts = null;

                _playerHashCts?.Cancel();
                _playerHashCts?.Dispose();
                _playerHashCts = null;
            }

            _disposed = true;
        }
    }
}