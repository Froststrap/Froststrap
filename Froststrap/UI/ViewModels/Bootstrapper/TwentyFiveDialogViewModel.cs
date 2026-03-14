using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using Avalonia.Media;

namespace Froststrap.UI.ViewModels.Bootstrapper
{
    public class TwentyFiveDialogViewModel : NotifyPropertyChangedViewModel
    {
        private readonly IBootstrapperDialog _dialog;

        public ICommand CancelInstallCommand => new RelayCommand(CancelInstall);

        public string Title => App.Settings.Prop.BootstrapperTitle;
        public IImage Icon { get; set; } = App.Settings.Prop.BootstrapperIcon.GetIcon().GetImageSource();
        public string Message { get; set; } = "Please wait...";
        public bool ProgressIndeterminate { get; set; } = true;
        public int ProgressMaximum { get; set; } = 0;
        public int ProgressValue { get; set; } = 0;

        public TaskbarItemProgressState TaskbarProgressState { get; set; } = TaskbarItemProgressState.Indeterminate;
        public double TaskbarProgressValue { get; set; } = 0;

        public bool CancelEnabled { get; set; } = false;
        public bool CancelButtonVisibility => CancelEnabled;

        [Obsolete("Do not use this! This is for the designer only.", true)]
        public TwentyFiveDialogViewModel()
        {
            _dialog = null!;
        }

        public TwentyFiveDialogViewModel(IBootstrapperDialog dialog)
        {
            _dialog = dialog;
        }

        private void CancelInstall()
        {
            _dialog.Bootstrapper?.Cancel();
            _dialog.CloseBootstrapper();
        }
    }
}