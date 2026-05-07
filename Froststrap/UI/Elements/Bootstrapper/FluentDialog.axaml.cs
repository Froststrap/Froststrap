using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Froststrap.UI.Elements.Bootstrapper.Base;
using Froststrap.UI.ViewModels.Bootstrapper;

namespace Froststrap.UI.Elements.Bootstrapper
{
    public partial class FluentDialog : AvaloniaDialogBase
    {
        private readonly FluentDialogViewModel? _viewModel;

        public FluentDialog()
        {
            InitializeComponent();
        }

        public FluentDialog(bool aero) : this()
        {
            string version = Utilities.GetRobloxVersionStr(Bootstrapper?.IsStudioLaunch ?? false);
            _viewModel = new FluentDialogViewModel(this, aero, version);
            DataContext = _viewModel;

            SetupDialog();

            var iconImage = App.Settings.Prop.BootstrapperIcon.GetIcon().GetImageSource();
            if (iconImage is Bitmap bitmap)
                Icon = new WindowIcon(bitmap);
        }

        #region UI Elements Overrides
        public override string Message
        {
            get => _viewModel!.Message;
            set => RunOnUI(() =>
            {
                _viewModel!.Message = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.Message));
            });
        }

        public override int ProgressMaximum
        {
            get => _viewModel!.ProgressMaximum;
            set => RunOnUI(() =>
            {
                _viewModel!.ProgressMaximum = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressMaximum));
            });
        }

        public override int ProgressValue
        {
            get => _viewModel!.ProgressValue;
            set => RunOnUI(() =>
            {
                _viewModel!.ProgressValue = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressValue));
            });
        }

        public override bool CancelEnabled
        {
            get => _viewModel!.CancelEnabled;
            set => RunOnUI(() =>
            {
                _viewModel!.CancelEnabled = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.CancelEnabled));
                _viewModel.OnPropertyChanged(nameof(_viewModel.CancelButtonVisible));
            });
        }

        public override bool ProgressIndeterminate
        {
            get => _viewModel!.ProgressIndeterminate;
            set => RunOnUI(() =>
            {
                _viewModel!.ProgressIndeterminate = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressIndeterminate));
            });
        }
        #endregion
    }
}