using System.ComponentModel;
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

        public string LaunchButtonText => SelectedLaunchMode == LaunchMode.Player ? "Launch Player" : "Launch Studio";
        public string LaunchButtonIcon => SelectedLaunchMode == LaunchMode.Player ? "PlayCircle" : "Wrench";

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