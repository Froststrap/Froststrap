// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Froststrap.UI.ViewModels.Dialogs
{
    internal class LaunchMenuViewModel : NotifyPropertyChangedViewModel
    {
        private LaunchMode _selectedLaunchMode = LaunchMode.Player;
        public LaunchMode SelectedLaunchMode
        {
            get => _selectedLaunchMode;
            set
            {
                if (_selectedLaunchMode != value)
                {
                    _selectedLaunchMode = value;
                    App.State.Prop.LastLaunchMode = value;
                    App.State.SaveSetting("LastLaunchMode");
                    OnPropertyChanged(nameof(SelectedLaunchMode));
                    OnPropertyChanged(nameof(LaunchButtonText));
                    OnPropertyChanged(nameof(LaunchButtonIcon));
                }
            }
        }

        private bool _isPlayerInstalled;
        public bool IsPlayerInstalled
        {
            get => _isPlayerInstalled;
            private set
            {
                if (_isPlayerInstalled == value)
                    return;

                _isPlayerInstalled = value;
                OnPropertyChanged(nameof(IsPlayerInstalled));
                OnPropertyChanged(nameof(LaunchButtonText));
            }
        }

        private bool _isStudioInstalled;
        public bool IsStudioInstalled
        {
            get => _isStudioInstalled;
            private set
            {
                if (_isStudioInstalled == value)
                    return;

                _isStudioInstalled = value;
                OnPropertyChanged(nameof(IsStudioInstalled));
                OnPropertyChanged(nameof(LaunchButtonText));
            }
        }

        public string PlayerMenuItemText => OperatingSystem.IsLinux()
            ? App.UseRosemary ? Strings.Common_Rosemary : Strings.Common_Sober
            : Strings.Common_Player;

        public string LaunchButtonText
        {
            get
            {
                if (SelectedLaunchMode == LaunchMode.Player)
                {
                    string modeName = OperatingSystem.IsLinux()
                        ? App.UseRosemary ? Strings.Common_Rosemary : Strings.Common_Sober
                        : Strings.Common_Player;
                    return IsPlayerInstalled
                        ? $"{Strings.Common_Launch} {modeName}"
                        : $"{Strings.Common_Install} {modeName}";
                }
                else
                {
                    return IsStudioInstalled
                        ? $"{Strings.Common_Launch} {Strings.Common_Studio}"
                        : $"{Strings.Common_Install} {Strings.Common_Studio}";
                }
            }
        }

        public string LaunchButtonIcon => SelectedLaunchMode == LaunchMode.Player ? "Gamepad2" : "Wrench";

        public ICommand LaunchCommand { get; }
        public ICommand SetLaunchModeCommand { get; }
        public ICommand LaunchSettingsCommand { get; }

        public event EventHandler<NextAction>? CloseWindowRequest;

        public LaunchMenuViewModel()
        {
            App.RuntimeChanged += RuntimeChanged;
            LaunchCommand = new RelayCommand(ExecuteLaunch);
            SetLaunchModeCommand = new RelayCommand<LaunchMode>(mode => SelectedLaunchMode = mode);
            LaunchSettingsCommand = new RelayCommand(() => CloseWindowRequest?.Invoke(this, NextAction.LaunchSettings));

            _selectedLaunchMode = App.State.Prop.LastLaunchMode;

            RefreshInstalledStates();

            if (!App.PlayerState.Loaded || !App.StudioState.Loaded)
                _ = LoadInstalledStatesAsync();
        }

        private async Task LoadInstalledStatesAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    _ = App.PlayerState.Prop;
                    _ = App.StudioState.Prop;
                });
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to load installation states");
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(RefreshInstalledStates);
        }

        private void RefreshInstalledStates()
        {
            if (OperatingSystem.IsLinux())
            {
                IsPlayerInstalled = App.LinuxPlayerInstalled;
            }
            else if (App.PlayerState.TryGetLoaded(out DistributionState playerState))
            {
                IsPlayerInstalled = App.PlayerData.IsInstalledWith(playerState);
            }

            if (App.StudioState.TryGetLoaded(out DistributionState studioState))
                IsStudioInstalled = App.StudioData.IsInstalledWith(studioState);
        }

        private void RuntimeChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(PlayerMenuItemText));
            OnPropertyChanged(nameof(LaunchButtonText));
        }

        private void ExecuteLaunch()
        {
            CloseWindowRequest?.Invoke(this, LaunchEntry.ToNextAction(SelectedLaunchMode));
        }

        public static string Version => string.Format(CultureInfo.InvariantCulture, Strings.Menu_About_Version, App.Version);
    }
}
