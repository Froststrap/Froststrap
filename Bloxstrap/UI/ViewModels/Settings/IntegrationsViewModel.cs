using Bloxstrap.Integrations;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class IntegrationsViewModel : NotifyPropertyChangedViewModel
    {
        public ICommand AddIntegrationCommand => new RelayCommand(AddIntegration);

        public ICommand DeleteIntegrationCommand => new RelayCommand(DeleteIntegration);

        public ICommand BrowseIntegrationLocationCommand => new RelayCommand(BrowseIntegrationLocation);

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

        private void BrowseIntegrationLocation()
        {
            if (SelectedCustomIntegration is null)
                return;

            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.Menu_AllFiles}|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            SelectedCustomIntegration.Name = dialog.SafeFileName;
            SelectedCustomIntegration.Location = dialog.FileName;
            OnPropertyChanged(nameof(SelectedCustomIntegration));
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
                    ShowServerUptimeEnabled = false;
                    AutoRejoinEnabled = false;
                    PlaytimeCounterEnabled = false;
                    DisableAppPatchEnabled = false;
                    DiscordActivityEnabled = false;
                    DiscordActivityJoinEnabled = false;
                    StudioRPCEnabled = false;

                    OnPropertyChanged(nameof(ShowServerDetailsEnabled));
                    OnPropertyChanged(nameof(ShowGameHistoryEnabled));
                    OnPropertyChanged(nameof(ShowServerUptimeEnabled));
                    OnPropertyChanged(nameof(AutoRejoinEnabled));
                    OnPropertyChanged(nameof(PlaytimeCounterEnabled));
                    OnPropertyChanged(nameof(DisableAppPatchEnabled));
                    OnPropertyChanged(nameof(DiscordActivityEnabled));
                    OnPropertyChanged(nameof(DiscordActivityJoinEnabled));
                    OnPropertyChanged(nameof(StudioRPCEnabled));
                }

                OnPropertyChanged(nameof(ActivityTrackingEnabled));
            }
        }

        public bool ShowServerDetailsEnabled
        {
            get => App.Settings.Prop.ShowServerDetails;
            set => App.Settings.Prop.ShowServerDetails = value;
        }

        public bool ShowServerUptimeEnabled
        {
            get => App.Settings.Prop.ShowServerUptime;
            set => App.Settings.Prop.ShowServerUptime = value;
        }

        public bool PlaytimeCounterEnabled
        {
            get => App.Settings.Prop.PlaytimeCounter;
            set => App.Settings.Prop.PlaytimeCounter = value;
        }

        public bool AutoRejoinEnabled
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

        private void OpenGameHistory()
        {
            try
            {
                var activityWatcher = new ActivityWatcher();

                var serverHistoryWindow = new Bloxstrap.UI.Elements.ContextMenu.ServerHistory(activityWatcher);
                serverHistoryWindow.Show();

                App.FrostRPC?.SetDialog("Game History");

                serverHistoryWindow.Closed += (s, e) =>
                {
                    activityWatcher?.Dispose();
                    App.FrostRPC?.ClearDialog();
                };
            }
            catch (Exception ex)
            {
                // Handle any errors
                MessageBox.Show($"Failed to open Game History: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public ObservableCollection<TrayDoubleClickAction> TrayDoubleClickActions { get; } =
            new ObservableCollection<TrayDoubleClickAction>(Enum.GetValues(typeof(TrayDoubleClickAction)).Cast<TrayDoubleClickAction>());

        public TrayDoubleClickAction SelectedDoubleClickAction
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

        public bool ShowUsingFroststrapRPC
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

        public bool DiscordActivityJoinEnabled
        {
            get => !App.Settings.Prop.HideRPCButtons;
            set => App.Settings.Prop.HideRPCButtons = !value;
        }

        public bool EnableCustomStatusDisplay
        {
            get => App.Settings.Prop.EnableCustomStatusDisplay;
            set => App.Settings.Prop.EnableCustomStatusDisplay = value;
        }

        public bool DiscordAccountOnProfile
        {
            get => App.Settings.Prop.ShowAccountOnRichPresence;
            set => App.Settings.Prop.ShowAccountOnRichPresence = value;
        }

        public bool DisableAppPatchEnabled
        {
            get => App.Settings.Prop.UseDisableAppPatch;
            set => App.Settings.Prop.UseDisableAppPatch = value;
        }

        public bool StudioRPCEnabled
        {
            get => App.Settings.Prop.StudioRPC;
            set
            {
                App.Settings.Prop.StudioRPC = value;

                if (!value)
                {
                    ThumbnailChanging = value;
                    EditingInfo = value;
                    WorkspaceInfo = value;
                    ShowTesting = value;
                    OnPropertyChanged(nameof(ThumbnailChanging));
                    OnPropertyChanged(nameof(EditingInfo));
                    OnPropertyChanged(nameof(WorkspaceInfo));
                    OnPropertyChanged(nameof(ShowTesting));
                }

                StudioPluginManager.Sync();
            }
        }

        public bool ThumbnailChanging
        {
            get => App.Settings.Prop.StudioThumbnailChanging;
            set => App.Settings.Prop.StudioThumbnailChanging = value;
        }

        public bool EditingInfo
        {
            get => App.Settings.Prop.StudioEditingInfo;
            set => App.Settings.Prop.StudioEditingInfo = value;
        }

        public bool WorkspaceInfo
        {
            get => App.Settings.Prop.StudioWorkspaceInfo;
            set => App.Settings.Prop.StudioWorkspaceInfo= value;
        }

        public bool ShowTesting
        {
            get => App.Settings.Prop.StudioShowTesting;
            set => App.Settings.Prop.StudioShowTesting = value;
        }

        public bool DisableRobloxRecording
        {
            get => IsBlocked(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Roblox"));
            set => SetBlockState(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Roblox"), value);
        }

        public bool DisableRobloxScreenshots
        {
            get => IsBlocked(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Roblox"));
            set => SetBlockState(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Roblox"), value);
        }

        public ObservableCollection<CustomIntegration> CustomIntegrations
        {
            get => App.Settings.Prop.CustomIntegrations;
            set => App.Settings.Prop.CustomIntegrations = value;
        }

        public CustomIntegration? SelectedCustomIntegration { get; set; }
        public int SelectedCustomIntegrationIndex { get; set; }
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

        private static void SetBlockState(string targetPath, bool block)
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
                        File.WriteAllBytes(targetPath, Array.Empty<byte>());
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
