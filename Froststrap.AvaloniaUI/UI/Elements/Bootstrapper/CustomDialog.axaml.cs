using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Froststrap.UI.Elements.Bootstrapper.Base;
using Froststrap.UI.ViewModels.Bootstrapper;
using Froststrap.UI.Elements.Base;

namespace Froststrap.UI.Elements.Bootstrapper
{
	public partial class CustomDialog : AvaloniaWindow, IBootstrapperDialog
	{
		private readonly BootstrapperDialogViewModel _viewModel;

		public ProgressBarStyle ProgressStyle { get; set; }
		public TaskbarItemProgressState TaskbarProgressState { get; set; }
		public double TaskbarProgressValue { get; set; }

		public Froststrap.Bootstrapper? Bootstrapper { get; set; }
		private bool _isClosing;

		#region UI Elements Properties
		public string Message
		{
			get => _viewModel.Message;
			set => _viewModel.Message = value;
		}

		public bool IsIndeterminate
		{
			get => _viewModel.ProgressIndeterminate;
			set => _viewModel.ProgressIndeterminate = value;
		}

		public int ProgressMaximum
		{
			get => _viewModel.ProgressMaximum;
			set => _viewModel.ProgressMaximum = value;
		}

		public int ProgressValue
		{
			get => _viewModel.ProgressValue;
			set => _viewModel.ProgressValue = value;
		}

		public void SetTaskbarProgress(double value, bool isPaused = false, bool isError = false)
		{
			TaskbarProgressValue = value;

			if (PlatformImpl is not null)
			{
				try
				{
					dynamic platform = PlatformImpl;
					platform.SetProgressBarValue(value);
				}
				catch
				{
					// Fallback for non Windows platforms
				}
			}
		}

		public bool CancelEnabled
		{
			get => _viewModel.CancelEnabled;
			set => _viewModel.CancelEnabled = value;
		}
		#endregion

		public CustomDialog()
		{
			InitializeComponent();

			_viewModel = new BootstrapperDialogViewModel(this);
			DataContext = _viewModel;

			Title = App.Settings.Prop.BootstrapperTitle;

			// Set Icon (Avalonia uses WindowIcon type)
			// Icon = new WindowIcon(App.Settings.Prop.BootstrapperIcon.GetIconStream());

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
		public void ShowBootstrapper()
		{
			this.Show();
		}

		public void CloseBootstrapper()
		{
			_isClosing = true;
			Dispatcher.UIThread.Post(this.Close);
		}

		public void ShowSuccess(string message, Action? callback)
			=> BaseFunctions.ShowSuccess(this, message, callback);
		#endregion
	}
}