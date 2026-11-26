// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using Froststrap.Localisation;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings;

internal sealed class IntegrationsViewModel : NotifyPropertyChangedViewModel
{
    public ICommand AddIntegrationCommand { get; } = new RelayCommand(AddIntegration);

    public ICommand DeleteIntegrationCommand { get; } = new RelayCommand<CustomIntegration>(integration =>
    {
        if (integration is not null)
            CustomIntegrations.Remove(integration);
    });

    public IAsyncRelayCommand<CustomIntegration> BrowseIntegrationLocationCommand { get; } = new AsyncRelayCommand<CustomIntegration>(BrowseIntegrationLocation);

    public ICommand OpenGameHistoryCommand => new RelayCommand(OpenGameHistory);

    private static void AddIntegration()
    {
        CustomIntegrations.Add(new CustomIntegration
        {
            Name = Strings.Menu_Integrations_Custom_NewIntegration,
            IsExpanded = true
        });
    }

    private static async Task BrowseIntegrationLocation(CustomIntegration? integration)
    {
        if (integration is null || Froststrap.UI.Elements.Settings.MainWindow.Instance is not { } window)
            return;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Integration File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("All files")
                {
                    Patterns = ["*"]
                }
            ]
        });

        if (files.Count == 0)
            return;

        string path = files[0].Path.LocalPath;

        if (string.IsNullOrWhiteSpace(integration.Name) || integration.Name == Strings.Menu_Integrations_Custom_NewIntegration)
            integration.Name = Path.GetFileNameWithoutExtension(path);

        integration.Location = path;
    }

    public bool ActivityTrackingEnabled
    {
        get => App.Settings.Prop.EnableActivityTracking;
        set
        {
            App.Settings.Prop.EnableActivityTracking = value;
            OnPropertyChanged(nameof(ActivityTrackingEnabled));
            StudioPluginManager.Sync();
        }
    }

    public bool PlaytimeTrackingEnabled
    {
        get => App.Settings.Prop.EnablePlaytimeTracking;
        set
        {
            App.Settings.Prop.EnablePlaytimeTracking = value;
            OnPropertyChanged(nameof(PlaytimeTrackingEnabled));
        }
    }

    public string PlaytimeTrackingLabel => Strings.Menu_Integrations_PlaytimeTracking_Title;

    public bool IsPlaytimeTrackingAvailable => App.Settings.Prop.AllowCookieAccess;

    public bool ShowServerDetailsEnabled
    {
        get => App.Settings.Prop.ShowServerDetails;
        set
        {
            App.Settings.Prop.ShowServerDetails = value;
            OnPropertyChanged(nameof(ShowServerDetailsEnabled));
        }
    }

    public static bool ShowServerUptimeEnabled
    {
        get => App.Settings.Prop.ShowServerUptime;
        set => App.Settings.Prop.ShowServerUptime = value;
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

    private async void OpenGameHistory()
    {
        var activityWatcher = new ActivityWatcher(App.Clients.Player);

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
            App.Logger.Error(ex, "Failed to change folder block state");
        }
    }
}
