using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Controls;
using Froststrap.UI.Elements.Bootstrapper.Base;
using Froststrap.UI.ViewModels.Bootstrapper;
using System;
using System.ComponentModel;

namespace Froststrap.UI.Elements.Bootstrapper
{
    public partial class ByfronDialog : AvaloniaDialogBase
    {
        private readonly ByfronDialogViewModel _viewModel;

        public new Froststrap.Bootstrapper? Bootstrapper { get; set; }

        private bool _isClosing;

        public ByfronDialog()
        {
            InitializeComponent();

            string version = Utilities.GetRobloxVersionStr(Bootstrapper?.IsStudioLaunch ?? false);
            _viewModel = new ByfronDialogViewModel(this, version);
            DataContext = _viewModel;

            Title = App.Settings.Prop.BootstrapperTitle;
            this.Closing += Window_Closing;

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

        private void Window_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (!_isClosing)
                Bootstrapper?.Cancel();
        }

        public override void ShowSuccess(string message, Action? callback) => BaseFunctions.ShowSuccess(message, callback);
        #endregion

        #region UI Elements (Synchronizing with ViewModel)
        public override string Message
        {
            get => _viewModel.Message;
            set
            {
                string message = value;
                if (message.EndsWith("..."))
                    message = message[..^3];

                _viewModel.Message = message;
                _viewModel.OnPropertyChanged(nameof(_viewModel.Message));
            }
        }

        public override int ProgressMaximum
        {
            get => _viewModel.ProgressMaximum;
            set
            {
                _viewModel.ProgressMaximum = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressMaximum));
            }
        }

        public override int ProgressValue
        {
            get => _viewModel.ProgressValue;
            set
            {
                _viewModel.ProgressValue = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressValue));
            }
        }

        public override bool CancelEnabled
        {
            get => _viewModel.CancelEnabled;
            set
            {
                _viewModel.CancelEnabled = value;

                _viewModel.OnPropertyChanged(nameof(_viewModel.CancelEnabled));
                _viewModel.OnPropertyChanged(nameof(_viewModel.CancelButtonVisible));
                _viewModel.OnPropertyChanged(nameof(_viewModel.VersionTextVisible));
                _viewModel.OnPropertyChanged(nameof(_viewModel.VersionText));
            }
        }
        #endregion
    }
}