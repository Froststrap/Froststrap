using Avalonia.Controls;
using Avalonia.Threading;
using Froststrap.UI.Elements.Bootstrapper.Base;
using Froststrap.UI.ViewModels.Bootstrapper;

namespace Froststrap.UI.Elements.Bootstrapper
{
	public partial class CustomDialog : AvaloniaDialogBase
    {
		private readonly BootstrapperDialogViewModel _viewModel;
        public new Froststrap.Bootstrapper? Bootstrapper { get; set; }
        private bool _isClosing;

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

        public CustomDialog()
		{
			InitializeComponent();

			_viewModel = new BootstrapperDialogViewModel(this);
			DataContext = _viewModel;
			Title = App.Settings.Prop.BootstrapperTitle;
			Icon = new WindowIcon(App.Settings.Prop.BootstrapperIcon.GetIcon());

			this.Closing += CustomDialog_Closing;
		}

		private void CustomDialog_Closing(object? sender, WindowClosingEventArgs e)
		{
			if (!_isClosing)
			{
				Bootstrapper?.Cancel();
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
    }
}