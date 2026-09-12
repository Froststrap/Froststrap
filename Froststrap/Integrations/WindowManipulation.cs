// Credits to fishstrap for originally making this

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Froststrap.Integrations
{
    internal class WindowManipulation : IDisposable
    {
        private const string LOG_IDENT = "WindowManipulation";
        private const uint WINEVENT_OUTOFCONTEXT = 0x0;
        private const uint EVENT_OBJECT_NAMECHANGE = 0x800C;
        private const uint WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        private const int ResolveTimeoutMs = 30_000;
        private const int ResolvePollMs = 750;
        private const int PostResolveDelayMs = 2_000;

        private WINEVENTPROC? _setTitleHook;
        private bool _titleHookInstalled;

        private HWND _hWnd;
        private readonly uint _robloxPID;
        private bool _disposed;

        private string _currentTitle = "Roblox";

        public WindowManipulation(long robloxProcessId)
        {
            App.Logger.Info(LOG_IDENT, $"Got Roblox PID {robloxProcessId}");

            _robloxPID = unchecked((uint)robloxProcessId);
        }

        public bool HasWindow => _hWnd != HWND.Null;

        public void Start()
        {
            _ = Task.Run(async () =>
            {
                if (!await WaitForWindowAsync())
                {
                    App.Logger.Warn(LOG_IDENT, $"Timed out waiting for window handle for PID {_robloxPID}");
                    return;
                }

                await Task.Delay(PostResolveDelayMs);

                if (_disposed)
                    return;

                _setTitleHook = new(SetWindowTitleHook);
                ApplyConfiguredIcon();
                ApplyConfiguredTitle();
            });
        }

        public void ApplyWindowModifications()
        {
            App.Logger.Info(LOG_IDENT, "Applying window modifications");

            _setTitleHook = new(SetWindowTitleHook);

            ApplyConfiguredIcon();
            ApplyConfiguredTitle();
        }

        public unsafe void ApplyConfiguredIcon()
        {
            if (!HasWindow)
                return;

            App.Logger.Info(LOG_IDENT, "Applying configured Roblox icon");

            RobloxIcon robloxIcon = App.Settings.Prop.RobloxIcon;

            if (robloxIcon == RobloxIcon.IconDefault)
            {
                PInvoke.SendMessage(_hWnd, WM_SETICON, new WPARAM(ICON_SMALL), new LPARAM(0));
                PInvoke.SendMessage(_hWnd, WM_SETICON, new WPARAM(ICON_BIG), new LPARAM(0));
                return;
            }

            IntPtr hIcon = robloxIcon.GetHIcon();
            if (hIcon == IntPtr.Zero)
                return;

            HICON hIconCopy = PInvoke.CopyIcon(new HICON(hIcon));

            PInvoke.SendMessage(_hWnd, WM_SETICON, new WPARAM(ICON_SMALL), new LPARAM((nint)hIconCopy.Value));
            PInvoke.SendMessage(_hWnd, WM_SETICON, new WPARAM(ICON_BIG), new LPARAM((nint)hIconCopy.Value));
        }

        public void ApplyConfiguredTitle()
        {
            if (!HasWindow)
                return;

            string robloxTitle = App.Settings.Prop.RobloxTitle;
            _currentTitle = robloxTitle;

            App.Logger.Info(LOG_IDENT, $"Applying configured Roblox title: {robloxTitle}");
            PInvoke.SetWindowText(_hWnd, robloxTitle);

            if (robloxTitle != "Roblox")
                EnsureTitleHook();
        }

        public unsafe void ApplyGameIcon(byte[] iconBytes)
        {
            if (!HasWindow)
                return;

            try
            {
                int smallWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSMICON);
                int smallHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSMICON);
                int bigWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXICON);
                int bigHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYICON);

                using var smallHandle = PInvoke.CreateIconFromResourceEx(
                    iconBytes, true, 0x00030000, smallWidth, smallHeight, IMAGE_FLAGS.LR_DEFAULTCOLOR);
                using var bigHandle = PInvoke.CreateIconFromResourceEx(
                    iconBytes, true, 0x00030000, bigWidth, bigHeight, IMAGE_FLAGS.LR_DEFAULTCOLOR);

                if (smallHandle.IsInvalid || bigHandle.IsInvalid)
                    return;

                HICON smallCopy = PInvoke.CopyIcon(new HICON(smallHandle.DangerousGetHandle()));
                HICON bigCopy = PInvoke.CopyIcon(new HICON(bigHandle.DangerousGetHandle()));

                PInvoke.SendMessage(_hWnd, WM_SETICON, new WPARAM(ICON_SMALL), new LPARAM((nint)smallCopy.Value));
                PInvoke.SendMessage(_hWnd, WM_SETICON, new WPARAM(ICON_BIG), new LPARAM((nint)bigCopy.Value));

                App.Logger.Info(LOG_IDENT, "Applied game icon to Roblox window");
            }
            catch (Exception ex)
            {
                App.Logger.Error(LOG_IDENT, $"Failed to apply game icon: {ex.Message}");
            }
        }

        public void ApplyGameTitle(string title)
        {
            if (!HasWindow)
                return;

            _currentTitle = title;
            EnsureTitleHook();

            App.Logger.Info(LOG_IDENT, $"Applying game title to Roblox window: {title}");
            PInvoke.SetWindowText(_hWnd, title);
        }

        public void ResetToConfigured()
        {
            App.Logger.Info(LOG_IDENT, "Resetting Roblox window back to configured state");

            if (App.Settings.Prop.AutoChangeIcon)
                ApplyConfiguredIcon();

            if (App.Settings.Prop.AutoChangeTitle)
                ApplyConfiguredTitle();
        }

        private async Task<bool> WaitForWindowAsync()
        {
            int elapsed = 0;

            while (!_disposed && elapsed < ResolveTimeoutMs)
            {
                if (TryGetWindowForPid((int)_robloxPID, out IntPtr handle))
                {
                    _hWnd = new HWND(handle);
                    App.Logger.Info(LOG_IDENT, $"Resolved window handle for PID {_robloxPID}: 0x{handle.ToInt64():X}");
                    return true;
                }

                await Task.Delay(ResolvePollMs);
                elapsed += ResolvePollMs;
            }

            return HasWindow;
        }

        private static bool TryGetWindowForPid(int pid, out IntPtr handle)
        {
            handle = IntPtr.Zero;
            if (pid <= 0)
                return false;

            try
            {
                using var proc = Process.GetProcessById(pid);
                proc.Refresh();

                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    handle = proc.MainWindowHandle;
                    return true;
                }
            }
            catch { }

            return false;
        }

        private void EnsureTitleHook()
        {
            if (_titleHookInstalled)
                return;

            WINEVENTPROC? hook = _setTitleHook;
            if (hook is null)
                return;

            _titleHookInstalled = true;

            PInvoke.SetWinEventHook(
                EVENT_OBJECT_NAMECHANGE,
                EVENT_OBJECT_NAMECHANGE,
                null,
                hook,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);
        }

        private void SetWindowTitleHook(
            HWINEVENTHOOK hWinEventHook,
            uint iEvent,
            HWND hWnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime)
        {
            if (!HasWindow || hWnd != _hWnd)
                return;

            Span<char> titleBuffer = new char[256];
            PInvoke.GetWindowText(_hWnd, titleBuffer);

            string newTitle = titleBuffer.TrimEnd('\0').ToString();

            if (newTitle != _currentTitle)
            {
                App.Logger.Info(LOG_IDENT, $"Reverting Roblox title back to: {_currentTitle}");
                PInvoke.SetWindowText(_hWnd, _currentTitle);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            GC.SuppressFinalize(this);
        }
    }
}