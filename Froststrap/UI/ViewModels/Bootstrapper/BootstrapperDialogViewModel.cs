using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;

namespace Froststrap.UI.ViewModels.Bootstrapper
{
    internal class BootstrapperDialogViewModel : NotifyPropertyChangedViewModel
    {
        private readonly IBootstrapperDialog _dialog;

        public ICommand CancelInstallCommand => new RelayCommand(CancelInstall);

        public static string Title => App.Settings.Prop.BootstrapperTitle;
        public IImage Icon { get; set; } = App.Settings.Prop.BootstrapperIcon.GetIcon().GetImageSource();
        public string Message { get; set; } = "Please wait...";
        public bool ProgressIndeterminate { get; set; } = true;
        public int ProgressMaximum { get; set; }
        public int ProgressValue { get; set; }

        public TaskbarItemProgressState TaskbarProgressState { get; set; } = TaskbarItemProgressState.Indeterminate;
        public double TaskbarProgressValue { get; set; }

        public bool CancelEnabled { get; set; }
        public bool CancelButtonVisible => CancelEnabled;

        [Obsolete("Do not use this! This is for the designer only.", true)]
        public BootstrapperDialogViewModel()
        {
            _dialog = null!;
        }

        public BootstrapperDialogViewModel(IBootstrapperDialog dialog)
        {
            _dialog = dialog;
        }

        private void CancelInstall()
        {
            bool keepOpen = _dialog.Bootstrapper?.Cancel() ?? false;
            if (!keepOpen)
                _dialog.CloseBootstrapper();
        }

        private string _cancelButtonText = Strings.Common_Cancel;
        public string CancelButtonText
        {
            get => _cancelButtonText;
            set
            {
                _cancelButtonText = value;
                OnPropertyChanged(nameof(CancelButtonText));
            }
        }
    }
}
