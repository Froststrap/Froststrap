using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Froststrap.UI.Elements.Bootstrapper.Base;
using Froststrap.UI.ViewModels.Bootstrapper;
using System;

namespace Froststrap.UI.Elements.Bootstrapper
{
    public partial class TwentyFiveDialog : AvaloniaDialogBase
    {
        private TwentyFiveDialogViewModel? _viewModel;
        public new Froststrap.Bootstrapper? Bootstrapper { get; set; }
        private bool _isClosing;

        public TwentyFiveDialog()
        {
            _viewModel = new TwentyFiveDialogViewModel(this);
            DataContext = _viewModel;

            InitializeComponent();

            this.Closing += Window_Closing;

            // Load Global Settings Icon
            var iconImage = App.Settings.Prop.BootstrapperIcon.GetIcon().GetImageSource();
            if (iconImage is Bitmap bitmap)
                Icon = new WindowIcon(bitmap);

            this.Closing += (s, e) => { if (!_isClosing) Bootstrapper?.Cancel(); };
        }


        private void Window_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (!_isClosing)
                Bootstrapper?.Cancel();
        }

        public new void ShowBootstrapper() => this.Show();

        public override void CloseBootstrapper()
        {
            _isClosing = true;
            Dispatcher.UIThread.Post(this.Close);
        }

        public override void ShowSuccess(string message, Action? callback) => BaseFunctions.ShowSuccess(message, callback);

        #region UI Elements Overrides
        public override string Message
        {
            get => _viewModel!.Message;
            set => RunOnUI(() =>
            {
                _viewModel!.Message = value;
                _viewModel!.OnPropertyChanged(nameof(_viewModel.Message));
            });
        }

        public override int ProgressMaximum
        {
            get => _viewModel!.ProgressMaximum;
            set => RunOnUI(() =>
            {
                _viewModel!.ProgressMaximum = value;
                _viewModel!.OnPropertyChanged(nameof(_viewModel.ProgressMaximum));
            });
        }

        public override int ProgressValue
        {
            get => _viewModel!.ProgressValue;
            set => RunOnUI(() =>
            {
                _viewModel!.ProgressValue = value;
                _viewModel!.OnPropertyChanged(nameof(_viewModel.ProgressValue));
            });
        }

        public override bool CancelEnabled
        {
            get => _viewModel!.CancelEnabled;
            set => RunOnUI(() =>
            {
                _viewModel!.CancelEnabled = value;
                _viewModel!.OnPropertyChanged(nameof(_viewModel.CancelEnabled));
            });
        }

        public override ProgressBarStyle ProgressStyle
        {
            get => _viewModel!.ProgressIndeterminate ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            set => RunOnUI(() =>
            {
                _viewModel!.ProgressIndeterminate = (value == ProgressBarStyle.Marquee);
                _viewModel!.OnPropertyChanged(nameof(_viewModel.ProgressIndeterminate));
            });
        }
        #endregion
    }
}