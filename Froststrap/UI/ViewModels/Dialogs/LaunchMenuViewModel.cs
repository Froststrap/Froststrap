using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Froststrap.UI.ViewModels.Dialogs
{
    public class LaunchMenuViewModel
    {
        public string Version => string.Format(Strings.Menu_About_Version, App.Version);

        public ICommand LaunchSettingsCommand => new RelayCommand(LaunchSettings);

        public ICommand LaunchRobloxCommand => new RelayCommand(LaunchRoblox);

        public ICommand LaunchRobloxStudioCommand => new RelayCommand(LaunchRobloxStudio);

        public ICommand LaunchAboutCommand => new RelayCommand(LaunchAbout);

        public event EventHandler? CloseWindowRequest;

        private void LaunchSettings()
        {
            Process.Start(Paths.Application, "-menu");
            CloseWindowRequest?.Invoke(this, EventArgs.Empty);
            App.FrostRPC?.Dispose();
        }

        private void LaunchRoblox() 
        {
            Process.Start(Paths.Application, "-player");
            CloseWindowRequest?.Invoke(this, EventArgs.Empty);
            App.FrostRPC?.Dispose();
        }

        private void LaunchRobloxStudio()
        {
            Process.Start(Paths.Application, "-studio");
            CloseWindowRequest?.Invoke(this, EventArgs.Empty);
            App.FrostRPC?.Dispose();
        }

        private void LaunchAbout() => new Elements.About.MainWindow().Show();
    }
}
