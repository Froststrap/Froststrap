using Avalonia.Controls;
using Avalonia.Threading;
using Froststrap.UI.Utility;
using Froststrap.UI.ViewModels.Bootstrapper;
using Avalonia.Media;

namespace Froststrap.UI.Elements.Bootstrapper.Base
{
    public class AvaloniaDialogBase : Window, IBootstrapperDialog
    {
        public const int TaskbarProgressMaximum = 100;
        public Froststrap.Bootstrapper? Bootstrapper { get; set; }
        protected bool IsClosing;

        #region UI Elements Backing Fields
        protected virtual string BackingMessage { get; set; } = "Please wait...";
        protected virtual int BackingProgressValue { get; set; }
        protected virtual int BackingProgressMaximum { get; set; }
        protected virtual bool BackingCancelEnabled { get; set; }
        protected virtual bool BackingProgressIndeterminate { get; set; }
        protected virtual double BackingTaskbarProgressValue { get; set; }
        protected virtual TaskbarItemProgressState BackingTaskbarProgressState { get; set; }
        #endregion

        #region UI Elements (Thread-Safe Properties)
        public virtual string Message
        {
            get => BackingMessage;
            set => RunOnUI(() => BackingMessage = value);
        }

        public virtual string CancelButtonText
        {
            get => ((BootstrapperDialogViewModel)DataContext!).CancelButtonText;
            set => RunOnUI(() => ((BootstrapperDialogViewModel)DataContext!).CancelButtonText = value);
        }

        public virtual int ProgressMaximum
        {
            get => BackingProgressMaximum;
            set => RunOnUI(() => BackingProgressMaximum = value);
        }

        public virtual int ProgressValue
        {
            get => BackingProgressValue;
            set => RunOnUI(() => BackingProgressValue = value);
        }

        public virtual bool CancelEnabled
        {
            get => BackingCancelEnabled;
            set => RunOnUI(() => BackingCancelEnabled = value);
        }

        public virtual bool ProgressIndeterminate
        {
            get => BackingProgressIndeterminate;
            set => RunOnUI(() => BackingProgressIndeterminate = value);
        }

        public virtual TaskbarItemProgressState TaskbarProgressState
        {
            get => BackingTaskbarProgressState;
            set => RunOnUI(() =>
            {
                BackingTaskbarProgressState = value;
                ApplyTaskbarState();
            });
        }

        public virtual double TaskbarProgressValue
        {
            get => BackingTaskbarProgressValue;
            set => RunOnUI(() =>
            {
                BackingTaskbarProgressValue = value;
                ApplyTaskbarState();
            });
        }
        #endregion

        public AvaloniaDialogBase()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            CanResize = false;

            this.WindowDecorations = WindowDecorations.None;

            TextOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);
            this.Closing += Dialog_Closing;
        }

        private void ApplyTaskbarState()
        {
            if (!OperatingSystem.IsWindows())
                return;

            IntPtr hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hwnd == IntPtr.Zero)
                return;

            try
            {
                TaskbarProgress.SetProgressState(hwnd, BackingTaskbarProgressState);

                if (BackingTaskbarProgressState == TaskbarItemProgressState.Normal)
                {
                    const int precision = 1000;
                    int completed = (int)Math.Clamp(BackingTaskbarProgressValue * precision, 0, precision);
                    TaskbarProgress.SetProgressValue(hwnd, completed, precision);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AvaloniaDialogBase", $"Taskbar Error: {ex.Message}");
            }
        }

        protected static void RunOnUI(Action action)
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
                FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
        }

        #region Event Handlers
        public void ButtonCancel_Click(object? sender, EventArgs e) => Close();

        private void Dialog_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (!IsClosing)
                Bootstrapper?.Cancel();
        }
        #endregion

        #region IBootstrapperDialog Methods
        public void ShowBootstrapper() => Show();

        public virtual void CloseBootstrapper()
        {
            RunOnUI(() =>
            {
                IsClosing = true;
                Close();
            });
        }

        public virtual void ShowSuccess(string message, Action? callback) =>
            BaseFunctions.ShowSuccess(message, callback);
        #endregion
    }
}