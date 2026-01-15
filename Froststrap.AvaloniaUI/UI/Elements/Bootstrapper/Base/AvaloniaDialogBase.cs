using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Froststrap.UI.Utility;
using Froststrap;
using Froststrap.UI.Elements.Bootstrapper.Base;

namespace Froststrap.UI.Elements.Bootstrapper.Base
{
	public class AvaloniaDialogBase : Window, IBootstrapperDialog
	{
		public const int TaskbarProgressMaximum = 100;

		public Froststrap.Bootstrapper? Bootstrapper { get; set; }

		private bool _isClosing;

		#region UI Elements (Properties with Thread Safety)
		protected virtual string _message { get; set; } = "Please wait...";
		protected virtual int _progressValue { get; set; }
		protected virtual int _progressMaximum { get; set; }
		protected virtual bool _cancelEnabled { get; set; }
		protected virtual double _taskbarProgressValue { get; set; }

		public string Message
		{
			get => _message;
			set => RunOnUI(() => _message = value);
		}

		public int ProgressMaximum
		{
			get => _progressMaximum;
			set => RunOnUI(() => _progressMaximum = value);
		}

		public int ProgressValue
		{
			get => _progressValue;
			set => RunOnUI(() => _progressValue = value);
		}

		public bool CancelEnabled
		{
			get => _cancelEnabled;
			set => RunOnUI(() => _cancelEnabled = value);
		}
        public ProgressBarStyle ProgressStyle { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TaskbarItemProgressState TaskbarProgressState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double TaskbarProgressValue { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        #endregion

        public AvaloniaDialogBase()
		{
			WindowStartupLocation = WindowStartupLocation.CenterScreen;
			CanResize = false;

			this.Closing += Dialog_Closing;
		}

		/// <summary>
		/// Replaces WinForms "InvokeRequired" logic with Avalonia Dispatcher
		/// </summary>
		private void RunOnUI(Action action)
		{
			if (Dispatcher.UIThread.CheckAccess())
				action();
			else
				Dispatcher.UIThread.Post(action);
		}

		public void SetupDialog()
		{
			Title = App.Settings.Prop.BootstrapperTitle;


			if (Locale.RightToLeft)
			{
				FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
			}
		}

		#region Event Handlers
		public void ButtonCancel_Click(object? sender, EventArgs e) => Close();

		private void Dialog_Closing(object? sender, WindowClosingEventArgs e)
		{
			if (!_isClosing)
			{
				Bootstrapper?.Cancel();
			}
		}
		#endregion

		#region IBootstrapperDialog Methods
		public void ShowBootstrapper() => ShowDialog(null);

		public virtual void CloseBootstrapper()
		{
			RunOnUI(() =>
			{
				_isClosing = true;
				Close();
			});
		}

		public virtual void ShowSuccess(string message, Action? callback)
			=> BaseFunctions.ShowSuccess(message, callback);
		#endregion
	}
}