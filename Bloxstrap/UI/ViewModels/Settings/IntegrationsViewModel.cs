using Bloxstrap.Integrations;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
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
                    AutoRejoinEnabled = false;
                    PlaytimeCounterEnabled = false;
                    DisableAppPatchEnabled = false;
                    DiscordActivityEnabled = false;
                    DiscordActivityJoinEnabled = false;

                    OnPropertyChanged(nameof(ShowServerDetailsEnabled));
                    OnPropertyChanged(nameof(ShowGameHistoryEnabled));
                    OnPropertyChanged(nameof(AutoRejoinEnabled));
                    OnPropertyChanged(nameof(PlaytimeCounterEnabled));
                    OnPropertyChanged(nameof(DisableAppPatchEnabled));
                    OnPropertyChanged(nameof(DiscordActivityEnabled));
                    OnPropertyChanged(nameof(DiscordActivityJoinEnabled));
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
            get => App.Settings.Prop.AutoRejoinEnabled;
            set => App.Settings.Prop.AutoRejoinEnabled = value;
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
            set
            {
                if (App.Settings.Prop.DoubleClickAction != value)
                {
                    App.Settings.Prop.DoubleClickAction = value;
                    OnPropertyChanged(nameof(SelectedDoubleClickAction));
                }
            }
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

        public bool DisableRobloxRecording
        {
            get => App.Settings.Prop.BlockRobloxRecording;
            set
            {
                App.Settings.Prop.BlockRobloxRecording = value;
                DisableRecording();
            }
        }

        public bool DisableRobloxScreenshots
        {
            get => App.Settings.Prop.BlockRobloxScreenshots;
            set
            {
                App.Settings.Prop.BlockRobloxScreenshots = value;
                DisableScreenshots();
            }
        }

        public ObservableCollection<CustomIntegration> CustomIntegrations
        {
            get => App.Settings.Prop.CustomIntegrations;
            set => App.Settings.Prop.CustomIntegrations = value;
        }

        public CustomIntegration? SelectedCustomIntegration { get; set; }
        public int SelectedCustomIntegrationIndex { get; set; }
        public bool IsCustomIntegrationSelected => SelectedCustomIntegration is not null;

        public static void DisableRecording()
        {
            const string LOG_IDENT = "Watcher::DisableRecording";
            string videosPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Roblox");
            string backupPath = videosPath + " (Before Blocking)";

            try
            {
                if (App.Settings.Prop.BlockRobloxRecording)
                {
                    if (Directory.Exists(videosPath))
                    {
                        bool hasContent = Directory.EnumerateFileSystemEntries(videosPath).Any();

                        if (hasContent)
                        {
                            if (!Directory.Exists(backupPath))
                                Directory.Move(videosPath, backupPath);
                        }
                        else
                        {
                            Directory.Delete(videosPath);
                        }
                    }

                    if (!File.Exists(videosPath))
                    {
                        File.WriteAllBytes(videosPath, Array.Empty<byte>());
                        File.SetAttributes(videosPath, FileAttributes.ReadOnly);
                    }
                }
                else
                {
                    if (File.Exists(videosPath) && !Directory.Exists(videosPath))
                    {
                        var attributes = File.GetAttributes(videosPath);
                        if ((attributes & FileAttributes.ReadOnly) != 0)
                        {
                            attributes &= ~FileAttributes.ReadOnly;
                            File.SetAttributes(videosPath, attributes);
                        }

                        File.Delete(videosPath);
                    }
                    if (!Directory.Exists(videosPath) && Directory.Exists(backupPath))
                    {
                        Directory.Move(backupPath, videosPath);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public static void DisableScreenshots()
        {
            const string LOG_IDENT = "Watcher::DisableScreenshots";
            string picturesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Roblox");
            string backupPath = picturesPath + " (Before Blocking)";

            try
            {
                if (App.Settings.Prop.BlockRobloxScreenshots)
                {
                    if (Directory.Exists(picturesPath))
                    {
                        bool hasContent = Directory.EnumerateFileSystemEntries(picturesPath).Any();

                        if (hasContent)
                        {
                            if (!Directory.Exists(backupPath))
                            {
                                Directory.Move(picturesPath, backupPath);
                                App.Logger.WriteLine(LOG_IDENT, $"Moved existing folder to '{backupPath}'");
                            }
                        }
                        else
                        {
                            Directory.Delete(picturesPath);
                            App.Logger.WriteLine(LOG_IDENT, $"Deleted empty folder '{picturesPath}'");
                        }
                    }

                    if (!File.Exists(picturesPath))
                    {
                        File.WriteAllBytes(picturesPath, Array.Empty<byte>());
                        File.SetAttributes(picturesPath, FileAttributes.ReadOnly);
                        App.Logger.WriteLine(LOG_IDENT, $"Created read-only file '{picturesPath}'");
                    }
                }
                else
                {
                    if (File.Exists(picturesPath) && !Directory.Exists(picturesPath))
                    {
                        var attributes = File.GetAttributes(picturesPath);
                        if ((attributes & FileAttributes.ReadOnly) != 0)
                        {
                            attributes &= ~FileAttributes.ReadOnly;
                            File.SetAttributes(picturesPath, attributes);
                        }

                        File.Delete(picturesPath);
                        App.Logger.WriteLine(LOG_IDENT, $"Deleted read-only file '{picturesPath}'");
                    }

                    if (!Directory.Exists(picturesPath) && Directory.Exists(backupPath))
                    {
                        Directory.Move(backupPath, picturesPath);
                        App.Logger.WriteLine(LOG_IDENT, $"Restored backup folder from '{backupPath}'");
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
