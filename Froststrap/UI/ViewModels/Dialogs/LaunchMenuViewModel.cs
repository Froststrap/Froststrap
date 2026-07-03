using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Froststrap.UI.ViewModels.Dialogs
{
    public class LaunchMenuViewModel : NotifyPropertyChangedViewModel
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
                    OnPropertyChanged(nameof(SelectedLaunchMode));
                    OnPropertyChanged(nameof(LaunchButtonText));
                    OnPropertyChanged(nameof(LaunchButtonIcon));
                }
            }
        }

        public static bool IsPlayerInstalled
        {
            get
            {
                if (OperatingSystem.IsLinux())
                {
                    var clientPath = Path.Combine(Paths.Versions, "Sober", "data", "sober", "packages", "x86_64", "com.roblox.client");
                    return Directory.Exists(clientPath) && Directory.EnumerateFiles(clientPath, "*", SearchOption.AllDirectories).Any();
                }
                else
                {
                    return App.IsPlayerInstalled;
                }
            }
        }

        public static bool IsStudioInstalled => App.IsStudioInstalled;

        public static string PlayerMenuItemText => OperatingSystem.IsLinux() ? Strings.Common_Sober : Strings.Common_Player;

        public string LaunchButtonText
        {
            get
            {
                if (SelectedLaunchMode == LaunchMode.Player)
                {
                    string modeName = OperatingSystem.IsLinux() ? Strings.Common_Sober : Strings.Common_Player;
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
        public ICommand LaunchAboutCommand { get; }

        public event EventHandler<NextAction>? CloseWindowRequest;

        public LaunchMenuViewModel()
        {
            LaunchCommand = new RelayCommand(ExecuteLaunch);
            SetLaunchModeCommand = new RelayCommand<LaunchMode>(mode => SelectedLaunchMode = mode);
            LaunchSettingsCommand = new RelayCommand(() => CloseWindowRequest?.Invoke(this, NextAction.LaunchSettings));
            LaunchAboutCommand = new RelayCommand(() => new Elements.About.MainWindow().Show());
        }

        private void ExecuteLaunch()
        {
            NextAction action = SelectedLaunchMode == LaunchMode.Player
                ? NextAction.LaunchRoblox
                : NextAction.LaunchRobloxStudio;
            CloseWindowRequest?.Invoke(this, action);
        }

        public static string Version => string.Format(Strings.Menu_About_Version, App.Version);
    }
}