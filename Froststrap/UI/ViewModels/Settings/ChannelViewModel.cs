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

        public IEnumerable<UpdateCheck> UpdateCheckValues => Enum.GetValues(typeof(UpdateCheck)).Cast<UpdateCheck>();

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

        private bool ValidateDomain(string domain)
        {
            const string domainPattern = @"^([a-zA-Z0-9.-]+)\.([a-zA-Z0-9]+)$";

            return Regex.IsMatch(domain, domainPattern);
        }

        public string RobloxDomain
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

        // TODO: Fix test mode
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

        public bool IsRobloxInstallationMissing => !App.IsPlayerInstalled && !App.IsStudioInstalled;

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
                    UserChannel? userChannel = await Deployment.GetUserChannel("WindowsPlayer");
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

                if (value.ToLower() == "live" || value.ToLower() == "zlive")
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

        public bool UpdateRoblox
        {
            get => App.Settings.Prop.UpdateRoblox && !IsRobloxInstallationMissing;
            set => App.Settings.Prop.UpdateRoblox = value;
        }

        public bool StaticDirectory
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
            OnPropertyChanged(nameof(StaticDirectory));
            OnPropertyChanged(nameof(TestModeEnabled));
            OnPropertyChanged(nameof(ViewChannel));
            OnPropertyChanged(nameof(IsRobloxInstallationMissing));
        }

        public IReadOnlyDictionary<string, ChannelChangeMode> ChannelChangeModes => new Dictionary<string, ChannelChangeMode>
        {
            { Strings.Menu_Channel_ChangeAction_Automatic, ChannelChangeMode.Automatic },
            { Strings.Menu_Channel_ChangeAction_Prompt, ChannelChangeMode.Prompt },
            { Strings.Menu_Channel_ChangeAction_Ignore, ChannelChangeMode.Ignore },
        };

        public string SelectedChannelChangeMode
        {
            get => ChannelChangeModes.FirstOrDefault(x => x.Value == App.Settings.Prop.ChannelChangeMode).Key;
            set => App.Settings.Prop.ChannelChangeMode = ChannelChangeModes[value];
        }

        public bool ForceRobloxReinstallation
        {
            get => App.State.Prop.ForceReinstall || IsRobloxInstallationMissing;
            set => App.State.Prop.ForceReinstall = value;
        }
    }
}