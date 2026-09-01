using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.Elements.Onboarding;

namespace Froststrap.UI.ViewModels.Onboarding
{
    internal class Page7ViewModel : NotifyPropertyChangedViewModel
    {
        public ICommand LaunchRobloxCommand { get; }
        public ICommand LaunchSettingsCommand { get; }

        public Page7ViewModel()
        {
            LaunchRobloxCommand = new RelayCommand(LaunchRoblox);
            LaunchSettingsCommand = new RelayCommand(LaunchSettings);
        }

        private void LaunchRoblox()
        {
            var mainWindow = MainWindow.Instance;
            if (mainWindow != null)
            {
                mainWindow.CloseAction = NextAction.LaunchRoblox;
                mainWindow.Close();
            }
        }

        private void LaunchSettings()
        {
            var mainWindow = MainWindow.Instance;
            if (mainWindow != null)
            {
                mainWindow.CloseAction = NextAction.LaunchSettings;
                mainWindow.Close();
            }
        }
    }
}
