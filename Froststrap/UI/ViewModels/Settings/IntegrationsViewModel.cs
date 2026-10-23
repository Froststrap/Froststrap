// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using Froststrap.Integrations.Discord;
using Froststrap.Models.Overlay;
using Froststrap.UI.Elements.Dialogs;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    internal class IntegrationsViewModel : NotifyPropertyChangedViewModel
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
                    DiscordActivityEnabled = false;
                    DiscordActivityJoinEnabled = false;
                    StudioRPCEnabled = false;

                    OnPropertyChanged(nameof(ShowServerDetailsEnabled));
                    OnPropertyChanged(nameof(ShowGameHistoryEnabled));
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
            set
            {
                App.Settings.Prop.ShowServerDetails = value;

                if (!value)
                {
                    ShowServerUptimeEnabled = false;

                    OnPropertyChanged(nameof(ShowServerUptimeEnabled));
                }

                OnPropertyChanged(nameof(ShowServerDetailsEnabled));
            }
        }

        public static bool ShowServerUptimeEnabled
        {
            get => App.Settings.Prop.ShowServerUptime;
            set => App.Settings.Prop.ShowServerUptime = value;
        }

        public bool EnableOverlay
        {
            get => App.Settings.Prop.EnableOverlay;
            set
            {
                App.Settings.Prop.EnableOverlay = value;
                OnPropertyChanged(nameof(EnableOverlay));
            }
        }

        public KeyModifiers OverlayHotkeyModifiers
        {
            get => App.Settings.Prop.OverlayHotkeyModifiers;
            set
            {
                if (App.Settings.Prop.OverlayHotkeyModifiers == value)
                    return;

                App.Settings.Prop.OverlayHotkeyModifiers = value;
                OnPropertyChanged(nameof(OverlayHotkeyModifiers));
                OverlayHotkey.NotifyChanged();
            }
        }

        public Key OverlayHotkeyKey
        {
            get => App.Settings.Prop.OverlayHotkeyKey;
            set
            {
                if (App.Settings.Prop.OverlayHotkeyKey == value)
                    return;

                App.Settings.Prop.OverlayHotkeyKey = value;
                OnPropertyChanged(nameof(OverlayHotkeyKey));
                OverlayHotkey.NotifyChanged();
            }
        }

        private bool _isRecordingHotkey;

        public bool IsRecordingHotkey
        {
            get => _isRecordingHotkey;
            set
            {
                if (_isRecordingHotkey == value)
                    return;

                _isRecordingHotkey = value;
                OnPropertyChanged(nameof(IsRecordingHotkey));
                OverlayHotkey.SetSuspended(value);
            }
        }

        public ICommand ResetHotkeyCommand => new RelayCommand(() =>
        {
            OverlayHotkeyModifiers = OverlayHotkey.DefaultModifiers;
            OverlayHotkeyKey = OverlayHotkey.DefaultKey;
        });

        public bool KeyOverlayEnabled
        {
            get => App.Settings.Prop.KeyOverlayEnabled;
            set
            {
                App.Settings.Prop.KeyOverlayEnabled = value;
                OnPropertyChanged(nameof(KeyOverlayEnabled));
            }
        }

        public KeyModifiers KeyOverlayHotkeyModifiers
        {
            get => App.Settings.Prop.KeyOverlayHotkeyModifiers;
            set
            {
                if (App.Settings.Prop.KeyOverlayHotkeyModifiers == value)
                    return;

                App.Settings.Prop.KeyOverlayHotkeyModifiers = value;
                OnPropertyChanged(nameof(KeyOverlayHotkeyModifiers));
                KeyOverlayHotkey.NotifyChanged();
            }
        }

        public Key KeyOverlayHotkeyKey
        {
            get => App.Settings.Prop.KeyOverlayHotkeyKey;
            set
            {
                if (App.Settings.Prop.KeyOverlayHotkeyKey == value)
                    return;

                App.Settings.Prop.KeyOverlayHotkeyKey = value;
                OnPropertyChanged(nameof(KeyOverlayHotkeyKey));
                KeyOverlayHotkey.NotifyChanged();
            }
        }

        private bool _isRecordingKeyOverlayHotkey;

        public bool IsRecordingKeyOverlayHotkey
        {
            get => _isRecordingKeyOverlayHotkey;
            set
            {
                if (_isRecordingKeyOverlayHotkey == value)
                    return;

                _isRecordingKeyOverlayHotkey = value;
                OnPropertyChanged(nameof(IsRecordingKeyOverlayHotkey));
                KeyOverlayHotkey.SetSuspended(value);
            }
        }

        public ICommand ResetKeyOverlayHotkeyCommand => new RelayCommand(() =>
        {
            KeyOverlayHotkeyModifiers = KeyOverlayHotkey.DefaultModifiers;
            KeyOverlayHotkeyKey = KeyOverlayHotkey.DefaultKey;
        });

        public KeyModifiers KeyOverlayMoveHotkeyModifiers
        {
            get => App.Settings.Prop.KeyOverlayMoveHotkeyModifiers;
            set
            {
                if (App.Settings.Prop.KeyOverlayMoveHotkeyModifiers == value)
                    return;

                App.Settings.Prop.KeyOverlayMoveHotkeyModifiers = value;
                OnPropertyChanged(nameof(KeyOverlayMoveHotkeyModifiers));
                KeyOverlayMoveHotkey.NotifyChanged();
            }
        }

        public Key KeyOverlayMoveHotkeyKey
        {
            get => App.Settings.Prop.KeyOverlayMoveHotkeyKey;
            set
            {
                if (App.Settings.Prop.KeyOverlayMoveHotkeyKey == value)
                    return;

                App.Settings.Prop.KeyOverlayMoveHotkeyKey = value;
                OnPropertyChanged(nameof(KeyOverlayMoveHotkeyKey));
                KeyOverlayMoveHotkey.NotifyChanged();
            }
        }

        private bool _isRecordingKeyOverlayMoveHotkey;

        public bool IsRecordingKeyOverlayMoveHotkey
        {
            get => _isRecordingKeyOverlayMoveHotkey;
            set
            {
                if (_isRecordingKeyOverlayMoveHotkey == value)
                    return;

                _isRecordingKeyOverlayMoveHotkey = value;
                OnPropertyChanged(nameof(IsRecordingKeyOverlayMoveHotkey));
                KeyOverlayMoveHotkey.SetSuspended(value);
            }
        }

        public ICommand ResetKeyOverlayMoveHotkeyCommand => new RelayCommand(() =>
        {
            KeyOverlayMoveHotkeyModifiers = KeyOverlayMoveHotkey.DefaultModifiers;
            KeyOverlayMoveHotkeyKey = KeyOverlayMoveHotkey.DefaultKey;
        });

        public IEnumerable<KeyOverlayStyle> KeyOverlayStyles { get; } = KeyOverlayStyleEx.Selections;

        public KeyOverlayStyle KeyOverlayStyle
        {
            get => App.Settings.Prop.KeyOverlayStyle;
            set
            {
                if (App.Settings.Prop.KeyOverlayStyle == value)
                    return;

                App.Settings.Prop.KeyOverlayStyle = value;
                OnPropertyChanged(nameof(KeyOverlayStyle));
                KeyOverlayDisplay.NotifyChanged();
            }
        }

        public IEnumerable<KeyOverlayScale> KeyOverlayScales { get; } = KeyOverlayScaleEx.Selections;

        public KeyOverlayScale KeyOverlayScale
        {
            get => App.Settings.Prop.KeyOverlayScale;
            set
            {
                if (App.Settings.Prop.KeyOverlayScale == value)
                    return;

                App.Settings.Prop.KeyOverlayScale = value;
                OnPropertyChanged(nameof(KeyOverlayScale));
                KeyOverlayDisplay.NotifyChanged();
            }
        }

        public bool KeyOverlayShowKeys
        {
            get => App.Settings.Prop.KeyOverlayShowKeys;
            set
            {
                App.Settings.Prop.KeyOverlayShowKeys = value;
                OnPropertyChanged(nameof(KeyOverlayShowKeys));
                KeyOverlayDisplay.NotifyChanged();
            }
        }

        public IEnumerable<SoftKeyProfile> SoftKeyProfiles { get; } = Enum.GetValues<SoftKeyProfile>();

        public SoftKeyProfile SoftKeyProfile
        {
            get => App.Settings.Prop.SoftKeyProfile;
            set
            {
                if (App.Settings.Prop.SoftKeyProfile == value)
                    return;

                App.Settings.Prop.SoftKeyProfile = value;
                OnPropertyChanged(nameof(SoftKeyProfile));
                KeyOverlayDisplay.NotifyChanged();
            }
        }

        public bool KeyOverlayShowMouse
        {
            get => App.Settings.Prop.KeyOverlayShowMouse;
            set
            {
                App.Settings.Prop.KeyOverlayShowMouse = value;
                OnPropertyChanged(nameof(KeyOverlayShowMouse));
                KeyOverlayDisplay.NotifyChanged();
            }
        }

        public bool KeyOverlayShowCpsLeft
        {
            get => App.Settings.Prop.KeyOverlayShowCpsLeft;
            set
            {
                App.Settings.Prop.KeyOverlayShowCpsLeft = value;
                OnPropertyChanged(nameof(KeyOverlayShowCpsLeft));
                KeyOverlayDisplay.NotifyChanged();
            }
        }

        public bool KeyOverlayShowCpsRight
        {
            get => App.Settings.Prop.KeyOverlayShowCpsRight;
            set
            {
                App.Settings.Prop.KeyOverlayShowCpsRight = value;
                OnPropertyChanged(nameof(KeyOverlayShowCpsRight));
                KeyOverlayDisplay.NotifyChanged();
            }
        }

        [SuppressMessage("Performance", "CA1822:Mark members as static",
            Justification = "Bound in XAML; must remain an instance member.")]
        public IBrush KeyOverlayHighlightColorBrush
        {
            get
            {
                string? stored = App.Settings.Prop.KeyOverlayHighlightColor;

                if (!string.IsNullOrWhiteSpace(stored) && Color.TryParse(stored, out var custom))
                    return new SolidColorBrush(custom);

                if (Application.Current?.TryFindResource("SystemAccentColor", out var accent) == true && accent is IBrush accentBrush)
                    return accentBrush;

                return new SolidColorBrush(Color.Parse("#0078D4"));
            }
        }

        private ICommand? _openKeyOverlayColorCommand;

        public ICommand OpenKeyOverlayColorCommand => _openKeyOverlayColorCommand ??= new RelayCommand<Control>(async control =>
        {
            if (control is null)
                return;

            var topLevel = TopLevel.GetTopLevel(control);

            if (topLevel is not Window parentWindow)
                return;

            string initial = App.Settings.Prop.KeyOverlayHighlightColor ?? string.Empty;

            if (string.IsNullOrWhiteSpace(initial)
                && Application.Current?.TryFindResource("SystemAccentColor", out var accent) == true
                && accent is ISolidColorBrush accentBrush)
            {
                initial = accentBrush.Color.ToString();
            }

            var dialog = new ColorPickerDialog(initial);
            var result = await dialog.ShowDialog<string>(parentWindow);

            if (string.IsNullOrWhiteSpace(result))
                return;

            App.Settings.Prop.KeyOverlayHighlightColor = result;
            OnPropertyChanged(nameof(KeyOverlayHighlightColorBrush));
            KeyOverlayDisplay.NotifyChanged();
        });

        public ICommand ResetKeyOverlayColorCommand => new RelayCommand(() =>
        {
            App.Settings.Prop.KeyOverlayHighlightColor = null;
            OnPropertyChanged(nameof(KeyOverlayHighlightColorBrush));
            KeyOverlayDisplay.NotifyChanged();
        });

        public bool SearchBarEnabled
        {
            get => App.Settings.Prop.SearchBarEnabled;
            set
            {
                App.Settings.Prop.SearchBarEnabled = value;
                OnPropertyChanged(nameof(SearchBarEnabled));
                OverlaySearchBarHotkey.NotifyChanged();
            }
        }

        public KeyModifiers SearchBarHotkeyModifiers
        {
            get => App.Settings.Prop.SearchBarHotkeyModifiers;
            set
            {
                if (App.Settings.Prop.SearchBarHotkeyModifiers == value)
                    return;

                App.Settings.Prop.SearchBarHotkeyModifiers = value;
                OnPropertyChanged(nameof(SearchBarHotkeyModifiers));
                OverlaySearchBarHotkey.NotifyChanged();
            }
        }

        public Key SearchBarHotkeyKey
        {
            get => App.Settings.Prop.SearchBarHotkeyKey;
            set
            {
                if (App.Settings.Prop.SearchBarHotkeyKey == value)
                    return;

                App.Settings.Prop.SearchBarHotkeyKey = value;
                OnPropertyChanged(nameof(SearchBarHotkeyKey));
                OverlaySearchBarHotkey.NotifyChanged();
            }
        }

        private bool _isRecordingSearchBarHotkey;

        public bool IsRecordingSearchBarHotkey
        {
            get => _isRecordingSearchBarHotkey;
            set
            {
                if (_isRecordingSearchBarHotkey == value)
                    return;

                _isRecordingSearchBarHotkey = value;
                OnPropertyChanged(nameof(IsRecordingSearchBarHotkey));
                OverlaySearchBarHotkey.SetSuspended(value);
            }
        }

        public ICommand ResetSearchBarHotkeyCommand => new RelayCommand(() =>
        {
            SearchBarHotkeyModifiers = OverlaySearchBarHotkey.DefaultModifiers;
            SearchBarHotkeyKey = OverlaySearchBarHotkey.DefaultKey;
        });


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
            set => BlockState(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Roblox"), value);
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
                App.Logger.Error("Unhandled exception: ", ex);
            }
        }
    }
}
