using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Froststrap.UI.Elements.Bootstrapper.Base;
using Froststrap.UI.ViewModels.Bootstrapper;

namespace Froststrap.UI.Elements.Bootstrapper
{
    public partial class TwentyFiveDialog : AvaloniaDialogBase
    {
        private readonly TwentyFiveDialogViewModel _viewModel;

        public TwentyFiveDialog()
        {
            _viewModel = new TwentyFiveDialogViewModel(this);
            DataContext = _viewModel;

            InitializeComponent();
            SetupDialog();

            // Load Global Settings Icon
            var iconImage = App.Settings.Prop.BootstrapperIcon.GetIcon().GetImageSource();
            if (iconImage is Bitmap bitmap)
                Icon = new WindowIcon(bitmap);
        }

        #region UI Elements Overrides
        public override string Message
        {
            get => _viewModel.Message;
            set => RunOnUI(() =>
            {
                _viewModel.Message = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.Message));
            });
        }

        public override int ProgressMaximum
        {
            get => _viewModel.ProgressMaximum;
            set => RunOnUI(() =>
            {
                _viewModel.ProgressMaximum = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressMaximum));
            });
        }

        public override int ProgressValue
        {
            get => _viewModel.ProgressValue;
            set => RunOnUI(() =>
            {
                _viewModel.ProgressValue = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressValue));
            });
        }

        public override bool CancelEnabled
        {
            get => _viewModel.CancelEnabled;
            set => RunOnUI(() =>
            {
                _viewModel.CancelEnabled = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.CancelEnabled));
            });
        }

        public override bool ProgressIndeterminate
        {
            get => _viewModel.ProgressIndeterminate;
            set => RunOnUI(() =>
            {
                _viewModel.ProgressIndeterminate = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressIndeterminate));
            });
        }
        #endregion
    }
}