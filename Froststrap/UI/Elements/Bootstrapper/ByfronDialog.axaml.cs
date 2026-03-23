using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Controls;
using Froststrap.UI.Elements.Bootstrapper.Base;
using Froststrap.UI.ViewModels.Bootstrapper;

namespace Froststrap.UI.Elements.Bootstrapper
{
    public partial class ByfronDialog : AvaloniaDialogBase
    {
        private readonly ByfronDialogViewModel _viewModel;

        public new Froststrap.Bootstrapper? Bootstrapper { get; set; }
        public ByfronDialog()
        {
            InitializeComponent();

            string version = Utilities.GetRobloxVersionStr(Bootstrapper?.IsStudioLaunch ?? false);
            _viewModel = new ByfronDialogViewModel(this, version);
            DataContext = _viewModel;

            Title = App.Settings.Prop.BootstrapperTitle;

            var iconImage = App.Settings.Prop.BootstrapperIcon.GetIcon().GetImageSource();
            if (iconImage is Bitmap bitmap)
                Icon = new WindowIcon(bitmap);

            if (App.Settings.Prop.Theme.GetFinal() == Enums.Theme.Light)
            {
                _viewModel.DialogBorder = new Thickness(1);
                _viewModel.Background = new SolidColorBrush(Color.FromRgb(242, 244, 245));
                _viewModel.Foreground = new SolidColorBrush(Color.FromRgb(57, 59, 61));
                _viewModel.IconColor = new SolidColorBrush(Color.FromRgb(57, 59, 61));
                _viewModel.ProgressBarBackground = new SolidColorBrush(Color.FromRgb(189, 190, 190));

                var uri = new Uri("avares://Froststrap/Resources/BootstrapperStyles/ByfronDialog/ByfronLogoLight.jpg");
                _viewModel.ByfronLogoLocation = new Bitmap(Avalonia.Platform.AssetLoader.Open(uri));
            }
        }

        #region IBootstrapperDialog Methods
        public new void ShowBootstrapper() => this.Show();

        public override void CloseBootstrapper()
        {
            _isClosing = true;
            Dispatcher.UIThread.Post(this.Close);
        }

        public override void ShowSuccess(string message, Action? callback) => BaseFunctions.ShowSuccess(message, callback);
        #endregion

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

        public override ProgressBarStyle ProgressStyle
        {
            get => _viewModel.ProgressIndeterminate ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            set => RunOnUI(() =>
            {
                _viewModel.ProgressIndeterminate = (value == ProgressBarStyle.Marquee);
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressIndeterminate));
            });
        }
        #endregion
    }
}