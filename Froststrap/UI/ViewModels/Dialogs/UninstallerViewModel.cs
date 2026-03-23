using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Froststrap.UI.ViewModels.Dialogs
{
    public class UninstallerViewModel : NotifyPropertyChangedViewModel
    {
        public string Text => String.Format(
            Strings.Uninstaller_Text, 
            "https://github.com/bloxstraplabs/bloxstrap/wiki/Roblox-crashes-or-does-not-launch",
            Paths.Base
        );

        private bool _keepData = true;
        public bool KeepData
        {
            get => _keepData;
            set
            {
                _keepData = value;
                OnPropertyChanged(nameof(KeepData));
            }
        }

        public ICommand ConfirmUninstallCommand => new RelayCommand(ConfirmUninstall);
        public ICommand CancelCommand => new RelayCommand(Cancel);

        public event EventHandler? ConfirmUninstallRequest;
        public event EventHandler? CancelRequest;

        private void ConfirmUninstall() => ConfirmUninstallRequest?.Invoke(this, EventArgs.Empty);

        private void Cancel() => CancelRequest?.Invoke(this, EventArgs.Empty);
    }
}