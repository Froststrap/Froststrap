// Credits to fishstrap for originally making this

using Avalonia.Threading;
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
        private const int OBJID_WINDOW = 0;

        private const int ResolveTimeoutMs = 30_000;
        private const int ResolvePollMs = 750;
        private const int PostResolveDelayMs = 2_000;

        private readonly WINEVENTPROC _setTitleHook;
        private bool _titleHookInstalled;
        private UnhookWinEventSafeHandle? _titleHook;

        private HWND _hWnd;
        private readonly uint _robloxPID;
        private bool _disposed;

        private string _currentTitle = "Roblox";
        private string _gameTitle = "";
        private string _configuredTitle = "Roblox";
        private bool _inGame;

        public WindowManipulation(long robloxProcessId)
        {
            App.Logger.Info(LOG_IDENT, $"Got Roblox PID {robloxProcessId}");

            _robloxPID = unchecked((uint)robloxProcessId);
            _setTitleHook = SetWindowTitleHook;
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

                ApplyConfiguredIcon();
                ApplyConfiguredTitle();
            });
        }

        public void ApplyWindowModifications()
        {
            App.Logger.Info(LOG_IDENT, "Applying window modifications");

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

            _configuredTitle = App.Settings.Prop.RobloxTitle;

            if (_inGame)
            {
                App.Logger.Info(LOG_IDENT, "In-game, keeping current title instead of applying configured title.");
                return;
            }

            _currentTitle = _configuredTitle;

            App.Logger.Info(LOG_IDENT, $"Applying configured Roblox title: {_configuredTitle}");
            PInvoke.SetWindowText(_hWnd, _configuredTitle);

            if (_configuredTitle != "Roblox")
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

            _inGame = true;
            _gameTitle = title;
            _currentTitle = title;

            EnsureTitleHook();

            App.Logger.Info(LOG_IDENT, $"Applying game title to Roblox window: {title}");
            PInvoke.SetWindowText(_hWnd, title);
        }

        public void ResetToConfigured()
        {
            App.Logger.Info(LOG_IDENT, "Resetting Roblox window back to configured state");

            _inGame = false;
            _gameTitle = "";

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

            Dispatcher.UIThread.Post(() =>
            {
                if (_titleHookInstalled || _disposed)
                    return;

                _titleHookInstalled = true;

                _titleHook = PInvoke.SetWinEventHook(
                    EVENT_OBJECT_NAMECHANGE,
                    EVENT_OBJECT_NAMECHANGE,
                    null,
                    _setTitleHook,
                    0,
                    0,
                    WINEVENT_OUTOFCONTEXT);

                App.Logger.Info(LOG_IDENT, "Title change hook installed.");
            });
        }

        private void UnhookTitleHook()
        {
            if (!_titleHookInstalled)
                return;

            try
            {
                _titleHook?.Dispose();
            }
            catch { }

            _titleHook = null;
            _titleHookInstalled = false;

            App.Logger.Info(LOG_IDENT, "Title change hook removed.");
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
            if (idObject != OBJID_WINDOW || idChild != 0)
                return;

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

            if (_inGame)
            {
                ApplyConfiguredIcon();
            }

            UnhookTitleHook();

            GC.SuppressFinalize(this);
        }
    }
}