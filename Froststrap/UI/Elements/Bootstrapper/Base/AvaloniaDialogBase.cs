using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Froststrap.UI.Utility;
using Froststrap;
using Froststrap.UI.Elements.Base;

namespace Froststrap.UI.Elements.Bootstrapper.Base
{
    public class AvaloniaDialogBase : AvaloniaWindow, IBootstrapperDialog
    {
        public const int TaskbarProgressMaximum = 100;

        public Froststrap.Bootstrapper? Bootstrapper { get; set; }

        private bool _isClosing;

        #region UI Elements Backing Fields
        protected virtual string _message { get; set; } = "Please wait...";
        protected virtual int _progressValue { get; set; }
        protected virtual int _progressMaximum { get; set; }
        protected virtual bool _cancelEnabled { get; set; }
        protected virtual double _taskbarProgressValue { get; set; }
        protected virtual ProgressBarStyle _progressStyle { get; set; }
        protected virtual TaskbarItemProgressState _taskbarProgressState { get; set; }
        #endregion

        #region UI Elements (Thread Safe Properties)
        public virtual string Message
        {
            get => _message;
            set => RunOnUI(() => _message = value);
        }

        public virtual int ProgressMaximum
        {
            get => _progressMaximum;
            set => RunOnUI(() => _progressMaximum = value);
        }

        public virtual int ProgressValue
        {
            get => _progressValue;
            set => RunOnUI(() => _progressValue = value);
        }

        public virtual bool CancelEnabled
        {
            get => _cancelEnabled;
            set => RunOnUI(() => _cancelEnabled = value);
        }

        public virtual ProgressBarStyle ProgressStyle
        {
            get => _progressStyle;
            set => RunOnUI(() => _progressStyle = value);
        }

        public virtual TaskbarItemProgressState TaskbarProgressState
        {
            get => _taskbarProgressState;
            set => RunOnUI(() => _taskbarProgressState = value);
        }

        public virtual double TaskbarProgressValue
        {
            get => _taskbarProgressValue;
            set => RunOnUI(() => _taskbarProgressValue = value);
        }
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
        protected void RunOnUI(Action action)
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
        public void ShowBootstrapper() => Show();

        public virtual void CloseBootstrapper()
        {
            RunOnUI(() =>
            {
                _isClosing = true;
                Close();
            });
        }

        public virtual void ShowSuccess(string message, Action? callback) => BaseFunctions.ShowSuccess(message, callback);
        #endregion
    }
}