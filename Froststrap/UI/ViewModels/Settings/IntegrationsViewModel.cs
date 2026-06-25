using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    public class IntegrationsViewModel : NotifyPropertyChangedViewModel
    {
        public ICommand AddIntegrationCommand => new RelayCommand(AddIntegration);

        public ICommand DeleteIntegrationCommand => new RelayCommand(DeleteIntegration);

        public IAsyncRelayCommand<Control> BrowseIntegrationLocationCommand => new AsyncRelayCommand<Control>(BrowseIntegrationLocation);

        public ICommand OpenGameHistoryCommand => new RelayCommand(OpenGameHistory);

        private void AddIntegration()
        {
            CustomIntegrations.Add(new CustomIntegration()
            {
                Name = Strings.Menu_Integrations_Custom_NewIntegration
            });

            SelectedCustomIntegrationIndex = CustomIntegrations.Count - 1;

            OnPropertyChanged(nameof(SelectedCustomIntegrationIndex));
            OnPropertyChanged(nameof(IsCustomIntegrationSelected));
        }

        private void DeleteIntegration()
        {
            if (SelectedCustomIntegration is null)
                return;

            CustomIntegrations.Remove(SelectedCustomIntegration);

            if (CustomIntegrations.Count > 0)
            {
                SelectedCustomIntegrationIndex = CustomIntegrations.Count - 1;
                OnPropertyChanged(nameof(SelectedCustomIntegrationIndex));
            }

            OnPropertyChanged(nameof(IsCustomIntegrationSelected));
        }

        private async Task BrowseIntegrationLocation(Control? control)
        {
            if (SelectedCustomIntegration is null) return;
            if (control is null) return;

            var topLevel = TopLevel.GetTopLevel(control);
            if (topLevel is not Window parentWindow) return;

            var storageProvider = parentWindow.StorageProvider;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Integration File",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("All files")
                    {
                        Patterns = ["*.*"]
                    }
                ]
            });

            if (files != null && files.Count > 0)
            {
                var file = files[0];
                string path = file.Path.LocalPath;

                if (string.IsNullOrWhiteSpace(SelectedCustomIntegration.Name) || SelectedCustomIntegration.Name == Strings.Menu_Integrations_Custom_NewIntegration)
                {
                    SelectedCustomIntegration.Name = Path.GetFileNameWithoutExtension(path);
                }

                SelectedCustomIntegration.Location = path;

                OnPropertyChanged(nameof(SelectedCustomIntegration));
            }
        }

        public bool ActivityTrackingEnabled
        {
            get => App.Settings.Prop.EnableActivityTracking;
            set
            {
                App.Settings.Prop.EnableActivityTracking = value;

                if (!value)
                {
                    ShowServerDetailsEnabled = false;
                    ShowGameHistoryEnabled = false;
                    AutoRejoinEnabled = false;
                    PlaytimeCounterEnabled = false;
                    DisableAppPatchEnabled = false;
                    AutoChangeTitle = false;
                    AutoChangeIcon = false;
                    DiscordActivityEnabled = false;
                    DiscordActivityJoinEnabled = false;
                    StudioRPCEnabled = false;

                    OnPropertyChanged(nameof(ShowServerDetailsEnabled));
                    OnPropertyChanged(nameof(ShowGameHistoryEnabled));
                    OnPropertyChanged(nameof(AutoRejoinEnabled));
                    OnPropertyChanged(nameof(PlaytimeCounterEnabled));
                    OnPropertyChanged(nameof(AutoChangeTitle));
                    OnPropertyChanged(nameof(AutoChangeIcon));
                    OnPropertyChanged(nameof(DisableAppPatchEnabled));
                    OnPropertyChanged(nameof(DiscordActivityEnabled));
                    OnPropertyChanged(nameof(DiscordActivityJoinEnabled));
                    OnPropertyChanged(nameof(StudioRPCEnabled));
                }

                OnPropertyChanged(nameof(ActivityTrackingEnabled));
            }
        }

        public static bool ShowServerDetailsEnabled
        {
            get => App.Settings.Prop.ShowServerDetails;
            set => App.Settings.Prop.ShowServerDetails = value;
        }

        public static bool PlaytimeCounterEnabled
        {
            get => App.Settings.Prop.PlaytimeCounter;
            set => App.Settings.Prop.PlaytimeCounter = value;
        }

        public static bool AutoRejoinEnabled
        {
            get => App.Settings.Prop.AutoRejoin;
            set => App.Settings.Prop.AutoRejoin = value;
        }

        public bool ShowGameHistoryEnabled
        {
            get => App.Settings.Prop.ShowGameHistoryMenu;
            set
            {
                App.Settings.Prop.ShowGameHistoryMenu = value;
                OnPropertyChanged(nameof(ShowGameHistoryEnabled));
            }
        }

        public static bool AutoChangeTitle
        {
            get => App.Settings.Prop.AutoChangeTitle;
            set => App.Settings.Prop.AutoChangeTitle = value;
        }

        public static bool AutoChangeIcon
        {
            get => App.Settings.Prop.AutoChangeIcon;
            set => App.Settings.Prop.AutoChangeIcon = value;
        }

        private async void OpenGameHistory()
        {
            var activityWatcher = new ActivityWatcher();

            var serverHistoryWindow = new Elements.ContextMenu.ServerHistory(activityWatcher);
            serverHistoryWindow.Show();

            App.FrostRPC?.SetDialog("Game History");

            serverHistoryWindow.Closed += (s, e) =>
            {
                activityWatcher?.Dispose();
                App.FrostRPC?.ClearDialog();
            };
        }

        public ObservableCollection<TrayDoubleClickAction> TrayDoubleClickActions { get; } = [.. Enum.GetValues<TrayDoubleClickAction>()];

        public static TrayDoubleClickAction SelectedDoubleClickAction
        {
            get => App.Settings.Prop.DoubleClickAction;
            set => App.Settings.Prop.DoubleClickAction = value;
        }

        public bool DiscordActivityEnabled
        {
            get => App.Settings.Prop.UseDiscordRichPresence;
            set
            {
                App.Settings.Prop.UseDiscordRichPresence = value;
                OnPropertyChanged(nameof(DiscordActivityEnabled));

                if (!value)
                {
                    DiscordActivityJoinEnabled = value;
                    EnableCustomStatusDisplay = value;
                    DiscordAccountOnProfile = value;
                    OnPropertyChanged(nameof(DiscordActivityJoinEnabled));
                    OnPropertyChanged(nameof(EnableCustomStatusDisplay));
                    OnPropertyChanged(nameof(DiscordAccountOnProfile));
                }
            }
        }

        public static bool ShowUsingFroststrapRPC
        {
            get => App.Settings.Prop.ShowUsingFroststrapRPC;
            set
            {
                App.Settings.Prop.ShowUsingFroststrapRPC = value;

                if (value)
                {
                    if (App.FrostRPC == null)
                    {
                        App.FrostRPC = new FroststrapRichPresence();
                        App.FrostRPC.SetPage("Integration");
                    }
                }
                else
                {
                    App.FrostRPC?.Dispose();
                    App.FrostRPC = null;
                }
            }
        }

        public static bool DiscordActivityJoinEnabled
        {
            get => !App.Settings.Prop.HideRPCButtons;
            set => App.Settings.Prop.HideRPCButtons = !value;
        }

        public static bool EnableCustomStatusDisplay
        {
            get => App.Settings.Prop.EnableCustomStatusDisplay;
            set => App.Settings.Prop.EnableCustomStatusDisplay = value;
        }

        public static bool DiscordAccountOnProfile
        {
            get => App.Settings.Prop.ShowAccountOnRichPresence;
            set => App.Settings.Prop.ShowAccountOnRichPresence = value;
        }

        public static bool DisableAppPatchEnabled
        {
            get => App.Settings.Prop.UseDisableAppPatch;
            set => App.Settings.Prop.UseDisableAppPatch = value;
        }

        public bool StudioRPCEnabled
        {
            get => App.Settings.Prop.StudioRPC;
            set => HandleStudioRPCChangeAsync(value);
        }

        private async void HandleStudioRPCChangeAsync(bool value)
        {
            if (value && !App.Settings.Prop.StudioRPC)
            {
                var result = await Frontend.ShowMessageBox(
                    Strings.Menu_Integrations_StudioRPC_PluginConfirmation,
                    MessageBoxImage.Information,
                    MessageBoxButton.YesNo
                );

                if (result != MessageBoxResult.Yes)
                {
                    OnPropertyChanged(nameof(StudioRPCEnabled));
                    return;
                }
            }

            App.Settings.Prop.StudioRPC = value;

            OnPropertyChanged(nameof(StudioRPCEnabled));

            if (!value)
            {
                ThumbnailChanging = value;
                EditingInfo = value;
                WorkspaceInfo = value;
                ShowTesting = value;
                StudioGameButton = value;
                OnPropertyChanged(nameof(ThumbnailChanging));
                OnPropertyChanged(nameof(EditingInfo));
                OnPropertyChanged(nameof(WorkspaceInfo));
                OnPropertyChanged(nameof(ShowTesting));
                OnPropertyChanged(nameof(StudioGameButton));
            }

            StudioPluginManager.Sync();
        }

        public static bool ThumbnailChanging
        {
            get => App.Settings.Prop.StudioThumbnailChanging;
            set => App.Settings.Prop.StudioThumbnailChanging = value;
        }

        public static bool EditingInfo
        {
            get => App.Settings.Prop.StudioEditingInfo;
            set => App.Settings.Prop.StudioEditingInfo = value;
        }

        public static bool WorkspaceInfo
        {
            get => App.Settings.Prop.StudioWorkspaceInfo;
            set => App.Settings.Prop.StudioWorkspaceInfo = value;
        }

        public static bool ShowTesting
        {
            get => App.Settings.Prop.StudioShowTesting;
            set => App.Settings.Prop.StudioShowTesting = value;
        }

        public static bool StudioGameButton
        {
            get => App.Settings.Prop.StudioGameButton;
            set => App.Settings.Prop.StudioGameButton = value;
        }

       
        public static bool DisableRobloxRecording
        {
            get => IsBlocked(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Roblox"));
            set =>BlockState(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Roblox"), value);
        }

        public static bool DisableRobloxScreenshots
        {
            get => IsBlocked(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Roblox"));
            set => BlockState(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Roblox"), value);
        }

        public static ObservableCollection<CustomIntegration> CustomIntegrations
        {
            get => App.Settings.Prop.CustomIntegrations;
            set => App.Settings.Prop.CustomIntegrations = value;
        }

        private CustomIntegration? _selectedCustomIntegration;
        public CustomIntegration? SelectedCustomIntegration
        {
            get => _selectedCustomIntegration;
            set
            {
                _selectedCustomIntegration = value;
                OnPropertyChanged(nameof(SelectedCustomIntegration));
                OnPropertyChanged(nameof(IsCustomIntegrationSelected));
            }
        }

        private int _selectedCustomIntegrationIndex = -1;
        public int SelectedCustomIntegrationIndex
        {
            get => _selectedCustomIntegrationIndex;
            set
            {
                _selectedCustomIntegrationIndex = value;
                OnPropertyChanged(nameof(SelectedCustomIntegrationIndex));
            }
        }

        public bool IsCustomIntegrationSelected => SelectedCustomIntegration is not null;

        private static bool IsBlocked(string path)
        {
            if (File.Exists(path) && !Directory.Exists(path))
            {
                var attr = File.GetAttributes(path);
                return attr.HasFlag(FileAttributes.ReadOnly);
            }
            return false;
        }

        private static void BlockState(string targetPath, bool block)
        {
            const string LOG_IDENT = "Watcher::SetBlockState";
            string backupPath = targetPath + " (Before Blocking)";

            try
            {
                if (block)
                {
                    if (Directory.Exists(targetPath))
                    {
                        if (Directory.EnumerateFileSystemEntries(targetPath).Any())
                        {
                            if (!Directory.Exists(backupPath)) Directory.Move(targetPath, backupPath);
                        }
                        else Directory.Delete(targetPath);
                    }

                    if (!File.Exists(targetPath))
                    {
                        File.WriteAllBytes(targetPath, []);
                        File.SetAttributes(targetPath, FileAttributes.ReadOnly);
                    }
                }
                else
                {
                    if (File.Exists(targetPath) && !Directory.Exists(targetPath))
                    {
                        var attr = File.GetAttributes(targetPath);
                        File.SetAttributes(targetPath, attr & ~FileAttributes.ReadOnly);
                        File.Delete(targetPath);
                    }

                    if (!Directory.Exists(targetPath) && Directory.Exists(backupPath))
                    {
                        Directory.Move(backupPath, targetPath);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }
    }
}
