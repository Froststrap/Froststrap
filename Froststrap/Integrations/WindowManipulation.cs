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

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const uint VK_RETURN = 0x0D;
        private const uint LLKHF_ALTDOWN = 0x20;

        private const int GWL_STYLE = -16;

        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_SYSMENU = 0x00080000;

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private readonly WINEVENTPROC _setTitleHook;
        private bool _titleHookInstalled;
        private UnhookWinEventSafeHandle? _titleHook;

        private HOOKPROC? _keyboardProc;
        private HHOOK? _keyboardHook;

        private HWND _hWnd;
        private readonly uint _robloxPID;
        private bool _disposed;

        private string _currentTitle = "Roblox";
        private string _configuredTitle = "Roblox";
        private bool _inGame;

        private RECT _storedWindowedRect;
        private bool _hasStoredWindowedRect;

        private int _storedWindowedStyle;
        private bool _hasStoredWindowedStyle;

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

                if (App.Settings.Prop.FullscreenMode == FullscreenMode.Borderless)
                    InstallKeyboardHook();
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
            _currentTitle = title;

            EnsureTitleHook();

            App.Logger.Info(LOG_IDENT, $"Applying game title to Roblox window: {title}");
            PInvoke.SetWindowText(_hWnd, title);
        }

        public void ResetToConfigured()
        {
            App.Logger.Info(LOG_IDENT, "Resetting Roblox window back to configured state");

            _inGame = false;

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

        private void InstallKeyboardHook()
        {
            if (_keyboardHook is not null || _disposed)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                if (_keyboardHook is not null || _disposed)
                    return;

                _keyboardProc = OnKeyboardEvent;

                _keyboardHook = PInvoke.SetWindowsHookEx(
                    WINDOWS_HOOK_ID.WH_KEYBOARD_LL,
                    _keyboardProc,
                    HINSTANCE.Null,
                    0);

                if (_keyboardHook is null || _keyboardHook.Value == IntPtr.Zero)
                {
                    App.Logger.Warn(LOG_IDENT, "Failed to install keyboard hook for Alt+Enter handling");
                    _keyboardHook = null;
                    _keyboardProc = null;
                }
                else
                {
                    App.Logger.Info(LOG_IDENT, "Keyboard hook installed (borderless Alt+Enter interception)");
                }
            });
        }

        private void UninstallKeyboardHook()
        {
            if (_keyboardHook is null)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (_keyboardHook is not null && _keyboardHook.Value != IntPtr.Zero)
                        PInvoke.UnhookWindowsHookEx(_keyboardHook.Value);
                }
                catch { }

                _keyboardHook = null;
                _keyboardProc = null;
            });
        }

        private unsafe LRESULT OnKeyboardEvent(int nCode, WPARAM wParam, LPARAM lParam)
        {
            if (nCode < 0)
                return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);

            uint msg = (uint)wParam.Value;
            if (msg != WM_KEYDOWN && msg != WM_SYSKEYDOWN)
                return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);

            if (!HasWindow
                || App.Settings.Prop.FullscreenMode != FullscreenMode.Borderless
                || PInvoke.GetForegroundWindow() != _hWnd)
            {
                return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);
            }

            var kb = (KBDLLHOOKSTRUCT*)lParam.Value;

            if (kb->vkCode != VK_RETURN)
                return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);

            if (((uint)kb->flags & LLKHF_ALTDOWN) == 0)
                return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);

            App.Logger.Info(LOG_IDENT, "Alt+Enter detected in borderless mode — toggling borderless fullscreen");

            Dispatcher.UIThread.Post(ToggleBorderlessFullscreen);

            return new LRESULT(1);
        }

        private bool IsCurrentlyBorderless()
        {
            if (!HasWindow)
                return false;

            int style = PInvoke.GetWindowLong(_hWnd, (WINDOW_LONG_PTR_INDEX)GWL_STYLE);
            return (style & WS_CAPTION) == 0 && (style & WS_THICKFRAME) == 0;
        }

        private void ToggleBorderlessFullscreen()
        {
            if (!HasWindow)
                return;

            try
            {
                if (IsCurrentlyBorderless())
                    MakeWindowed();
                else
                    MakeBorderless();
            }
            catch (Exception ex)
            {
                App.Logger.Error(LOG_IDENT, "Failed to toggle borderless fullscreen");
                App.Logger.Error(ex);
            }
        }

        private void MakeBorderless()
        {
            if (!HasWindow)
                return;

            int style = PInvoke.GetWindowLong(_hWnd, (WINDOW_LONG_PTR_INDEX)GWL_STYLE);

            if (PInvoke.GetWindowRect(_hWnd, out RECT current))
            {
                _storedWindowedRect = current;
                _hasStoredWindowedRect = true;
            }

            _storedWindowedStyle = style;
            _hasStoredWindowedStyle = true;

            int newStyle = style;
            newStyle &= ~WS_CAPTION;
            newStyle &= ~WS_THICKFRAME;
            newStyle &= ~WS_MINIMIZEBOX;
            newStyle &= ~WS_MAXIMIZEBOX;
            newStyle &= ~WS_SYSMENU;

            _ = PInvoke.SetWindowLong(_hWnd, (WINDOW_LONG_PTR_INDEX)GWL_STYLE, newStyle);

            int screenW = PInvoke.GetSystemMetrics((SYSTEM_METRICS_INDEX)SM_CXSCREEN);
            int screenH = PInvoke.GetSystemMetrics((SYSTEM_METRICS_INDEX)SM_CYSCREEN);

            _ = PInvoke.SetWindowPos(
                _hWnd,
                default,
                0,
                0,
                screenW,
                screenH + 1,
                SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED |
                SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW |
                SET_WINDOW_POS_FLAGS.SWP_NOZORDER);

            App.Logger.Info(LOG_IDENT, "Borderless toggled → fullscreen");
        }

        private void MakeWindowed()
        {
            if (!HasWindow)
                return;

            int currentStyle = PInvoke.GetWindowLong(_hWnd, (WINDOW_LONG_PTR_INDEX)GWL_STYLE);
            int restoreStyle = _hasStoredWindowedStyle ? _storedWindowedStyle : currentStyle;
            _hasStoredWindowedStyle = false;

            _ = PInvoke.SetWindowLong(_hWnd, (WINDOW_LONG_PTR_INDEX)GWL_STYLE, restoreStyle);

            RECT target;
            if (_hasStoredWindowedRect)
            {
                target = _storedWindowedRect;
                _hasStoredWindowedRect = false;
            }
            else
            {
                int screenW = PInvoke.GetSystemMetrics((SYSTEM_METRICS_INDEX)SM_CXSCREEN);
                int screenH = PInvoke.GetSystemMetrics((SYSTEM_METRICS_INDEX)SM_CYSCREEN);
                const int width = 1280;
                const int height = 720;
                int x = Math.Max(0, (screenW - width) / 2);
                int y = Math.Max(0, (screenH - height) / 2);
                target = new RECT
                {
                    left = x,
                    top = y,
                    right = x + width,
                    bottom = y + height
                };
            }

            _ = PInvoke.SetWindowPos(
                _hWnd,
                default,
                target.left,
                target.top,
                target.right - target.left,
                target.bottom - target.top,
                SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
                SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);

            App.Logger.Info(LOG_IDENT, "Borderless toggled → windowed");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            UninstallKeyboardHook();

            if (_inGame)
            {
                ApplyConfiguredIcon();
            }

            UnhookTitleHook();

            GC.SuppressFinalize(this);
        }
    }
}