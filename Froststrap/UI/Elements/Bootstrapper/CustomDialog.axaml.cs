using Avalonia.Controls;
using AnimatedImage.Avalonia;
using Froststrap.UI.Elements.Bootstrapper.Base;
using Froststrap.UI.ViewModels.Bootstrapper;

namespace Froststrap.UI.Elements.Bootstrapper
{
    public partial class CustomDialog : AvaloniaDialogBase
    {
        private readonly BootstrapperDialogViewModel _viewModel;

        public CustomDialog()
        {
            InitializeComponent();

            _viewModel = new BootstrapperDialogViewModel(this);
            DataContext = _viewModel;

            SetupDialog();

            Icon = new WindowIcon(App.Settings.Prop.BootstrapperIcon.GetIcon());

            this.Loaded += (s, e) =>
            {
                RootTitleBar.PointerPressed += (sender, args) =>
                {
                    BeginMoveDrag(args);
                    args.Handled = true;
                };
            };

            this.Closing += CustomDialog_Closing;
        }

        private void CustomDialog_Closing(object? sender, WindowClosingEventArgs e)
        {
            foreach (var image in _animatedImages)
            {
                var animatedSource = image.GetValue(ImageBehavior.AnimatedSourceProperty);
                if (animatedSource is AnimatedImageSourceStream streamSource && streamSource.StreamSource is MemoryStream ms)
                {
                    ms.Dispose();
                }
                image.ClearValue(ImageBehavior.AnimatedSourceProperty);
            }
            _animatedImages.Clear();
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
                _viewModel.OnPropertyChanged(nameof(_viewModel.CancelButtonVisible));
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