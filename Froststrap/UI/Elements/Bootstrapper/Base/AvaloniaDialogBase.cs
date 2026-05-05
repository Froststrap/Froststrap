using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.Utility;

namespace Froststrap.UI.Elements.Bootstrapper.Base
{
    public class AvaloniaDialogBase : AvaloniaWindow, IBootstrapperDialog
    {
        public const int TaskbarProgressMaximum = 100;

        public Froststrap.Bootstrapper? Bootstrapper { get; set; }

        protected bool _isClosing;

        #region Taskbar COM P/Invoke (ITaskbarList3)
        [ComImport]
        [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3
        {
            void HrInit();
            void AddTab(IntPtr hwnd);
            void DeleteTab(IntPtr hwnd);
            void ActivateTab(IntPtr hwnd);
            void SetActiveAlt(IntPtr hwnd);
            void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
            void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
            void SetProgressState(IntPtr hwnd, TBPFLAG tbpFlags);
        }

        [ComImport]
        [Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
        [ClassInterface(ClassInterfaceType.None)]
        private class TaskbarInstance { }

        private enum TBPFLAG : int
        {
            TBPF_NOPROGRESS = 0x0,
            TBPF_INDETERMINATE = 0x1,
            TBPF_NORMAL = 0x2,
            TBPF_ERROR = 0x4,
            TBPF_PAUSED = 0x8,
        }

        private ITaskbarList3? _taskbarList;

        private ITaskbarList3? GetTaskbarList()
        {
            if (!OperatingSystem.IsWindows())
                return null;

            if (_taskbarList is null)
            {
                try
                {
                    _taskbarList = (ITaskbarList3)new TaskbarInstance();
                    _taskbarList.HrInit();
                }
                catch
                {
                    _taskbarList = null;
                }
            }

            return _taskbarList;
        }

        private IntPtr GetHwnd()
        {
            var platformHandle = TryGetPlatformHandle();
            return platformHandle?.Handle ?? IntPtr.Zero;
        }

        private static TBPFLAG ToTbpFlag(TaskbarItemProgressState state) => state switch
        {
            TaskbarItemProgressState.Normal => TBPFLAG.TBPF_NORMAL,
            TaskbarItemProgressState.Indeterminate => TBPFLAG.TBPF_INDETERMINATE,
            TaskbarItemProgressState.Error => TBPFLAG.TBPF_ERROR,
            TaskbarItemProgressState.Paused => TBPFLAG.TBPF_PAUSED,
            _ => TBPFLAG.TBPF_NOPROGRESS,
        };
        #endregion

        #region UI Elements Backing Fields
        protected virtual string _message { get; set; } = "Please wait...";
        protected virtual int _progressValue { get; set; }
        protected virtual int _progressMaximum { get; set; }
        protected virtual bool _cancelEnabled { get; set; }
        protected virtual bool _progressIndeterminate { get; set; }
        protected virtual double _taskbarProgressValue { get; set; }
        protected virtual TaskbarItemProgressState _taskbarProgressState { get; set; }
        #endregion

        #region UI Elements (Thread-Safe Properties)
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

        public virtual bool ProgressIndeterminate
        {
            get => _progressIndeterminate;
            set => RunOnUI(() => _progressIndeterminate = value);
        }

        public virtual TaskbarItemProgressState TaskbarProgressState
        {
            get => _taskbarProgressState;
            set => RunOnUI(() =>
            {
                _taskbarProgressState = value;
                ApplyTaskbarState();
            });
        }

        public virtual double TaskbarProgressValue
        {
            get => _taskbarProgressValue;
            set => RunOnUI(() =>
            {
                _taskbarProgressValue = value;
                ApplyTaskbarState();
            });
        }
        #endregion

        public AvaloniaDialogBase()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            CanResize = false;

            this.Closing += Dialog_Closing;
        }

        private void ApplyTaskbarState()
        {
            if (!OperatingSystem.IsWindows())
                return;

            var taskbar = GetTaskbarList();
            if (taskbar is null)
                return;

            IntPtr hwnd = GetHwnd();
            if (hwnd == IntPtr.Zero)
                return;

            try
            {
                taskbar.SetProgressState(hwnd, ToTbpFlag(_taskbarProgressState));

                const ulong total = 1000;
                ulong completed = (ulong)Math.Clamp(_taskbarProgressValue * total, 0, total);
                taskbar.SetProgressValue(hwnd, completed, total);
            }
            catch
            {
            }
        }

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
                FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
        }

        #region Event Handlers
        public void ButtonCancel_Click(object? sender, EventArgs e) => Close();

        private void Dialog_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (!_isClosing)
                Bootstrapper?.Cancel();
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

        public virtual void ShowSuccess(string message, Action? callback) =>
            BaseFunctions.ShowSuccess(message, callback);
        #endregion
    }
}