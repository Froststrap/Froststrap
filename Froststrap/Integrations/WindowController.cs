using System.Runtime.InteropServices;
using Message = Froststrap.Models.BloxstrapRPC.Message;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Avalonia.Threading;

namespace Froststrap.Integrations
{
    public struct WindowRect
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
    }

    public partial class WindowController : IDisposable
    {
        private readonly ActivityWatcher _activityWatcher;
        private UI.Elements.ContextMenu.MenuContainer? _menuContainer;

        private IntPtr _currentWindow;
        private long _windowLong;
        private bool _foundWindow;
        private bool _enabled;

        public const uint WM_SETTEXT = 0x000C;
        public const int GWL_EXSTYLE = -20;
        public const long WS_EX_LAYERED = 0x00080000L;
        public const long WS_EX_TRANSPARENT = 0x00000020L;
        public const uint LWA_COLORKEY = 0x00000001;
        public const uint LWA_ALPHA = 0x00000002;
        public const long WS_BORDER = 0x00800000L;
        public const long WS_CAPTION = 0x00C00000L;
        private const int GWL_STYLE = -16;
        private const long WS_THICKFRAME = 0x00040000L;
        private const long WS_SYSMENU = 0x00080000L;
        private const int GWL_EXSTYLE_32 = -20;
        private const long WS_MINIMIZEBOX = 0x00020000L;
        private const long WS_MAXIMIZEBOX = 0x00010000L;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public void SetBorderless(bool borderless)
        {
            if (_currentWindow == IntPtr.Zero) return;

            long style = GetWindowLong(_currentWindow, GWL_STYLE);

            if (borderless)
            {
                style &= ~WS_CAPTION;
                style &= ~WS_THICKFRAME;
                style &= ~WS_BORDER;
                style &= ~WS_SYSMENU;
                style &= ~WS_MINIMIZEBOX;
                style &= ~WS_MAXIMIZEBOX;
            }
            else
            {
                style |= WS_CAPTION;
                style |= WS_THICKFRAME;
                style |= WS_SYSMENU;
                style |= WS_MINIMIZEBOX;
                style |= WS_MAXIMIZEBOX;
            }

            _ = SetWindowLong(_currentWindow, GWL_STYLE, style);
            _ = SetWindowPos(_currentWindow, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }

        private const int defaultScreenWidth = 1280;
        private const int defaultScreenHeight = 720;

        public int monitorX;
        public int monitorY;

        public float widthMult = 1;
        public float heightMult = 1;

        private int screenWidth;
        private int screenHeight;

        private bool changedWindow;

        private int _lastX;
        private int _lastY;
        private int _lastWidth;
        private int _lastHeight;
        private int _lastSCWidth;
        private int _lastSCHeight;
        private byte _lastTransparency = 1;
        private uint _lastWindowColor;
        private uint _lastWindowCaptionColor;
        private uint _lastWindowBorderColor;
        private uint _lastTransparencyMode = 0x00000001;

        private int _startingX;
        private int _startingY;
        private int _startingWidth;
        private int _startingHeight;

        private bool curUniverseAllowed;
        private long prevUniverse;

        private Theme appTheme = Theme.Default;
        private const int S_OK = 0;

        public WindowController(ActivityWatcher activityWatcher)
        {
            _activityWatcher = activityWatcher;
            _activityWatcher.OnRPCMessage += (_, message) => OnMessage(message);
            _activityWatcher.OnGameLeave += (_, _) => { prevUniverse = 0; StopWindow(); };
            _activityWatcher.OnGameJoin += (_, _) => UpdateExposedPerms();

            _lastSCWidth = defaultScreenWidth;
            _lastSCHeight = defaultScreenHeight;

            _currentWindow = FindWindow();
            _foundWindow = _currentWindow != IntPtr.Zero;

            if (_foundWindow) { OnWindowFound(); }

            UpdateExposedPerms();
        }

        public void RequestPermission(long universeId = -1)
        {
            if (universeId == -1) { universeId = _activityWatcher.Data.UniverseId; }
            if (App.Settings.Prop.WindowAllowedUniverses.Contains(universeId)) { return; }
            if (App.Settings.Prop.WindowBlacklistedUniverses.Contains(universeId)) { return; }
            if (prevUniverse == universeId) { return; }
            prevUniverse = universeId;

            _menuContainer ??= _activityWatcher.watcher._notifyIcon?._menuContainer;

            if (_menuContainer != null)
            {
                Dispatcher.UIThread.Invoke(delegate
                {
                    _menuContainer.ShowWindowPermissionWindow();
                });
            }
        }

        public void UpdateExposedPerms()
        {
            if (Watcher.RobloxPath == null) { return; }

            var idsPath = Path.Combine(Watcher.RobloxPath, "content\\bloxstrap");
            if (Directory.Exists(idsPath))
            {
                var directory = new DirectoryInfo(idsPath);
                foreach (FileInfo file in directory.GetFiles()) if (file.Name != "enabled.png") file.Delete();
            }
            else { Directory.CreateDirectory(idsPath); }

            var currentUniverse = _activityWatcher.Data.UniverseId;

            curUniverseAllowed = App.Settings.Prop.WindowAllowAll || IsGameAllowed(currentUniverse);
            if (!curUniverseAllowed) { return; }

            using Image<Rgba32> bitmap = new(3, 1);
            bitmap[0, 0] = App.Settings.Prop.MoveWindowAllowed ? Color.White : Color.Transparent;
            bitmap[1, 0] = App.Settings.Prop.TitleControlAllowed ? Color.White : Color.Transparent;
            bitmap[2, 0] = App.Settings.Prop.WindowTransparencyAllowed ? Color.White : Color.Transparent;

            bitmap.Save(Path.Combine(idsPath, $"{currentUniverse}.png"));
        }

        public bool IsGameAllowed(long universeId = -1)
        {
            if (universeId == -1) { universeId = _activityWatcher.Data.UniverseId; }
            return App.Settings.Prop.WindowAllowedUniverses.Contains(universeId);
        }

        public void UpdateState(bool state)
        {
            _enabled = state;
            if (!_enabled)
            {
                StopWindow();
            }
        }

        public void UpdateWinMonitor()
        {
            if (App.Settings.Prop.WindowMonitorStyle == WindowMonitorStyle.All)
            {
                screenWidth = GetSystemMetrics(78);
                screenHeight = GetSystemMetrics(79);
                monitorX = GetSystemMetrics(76);
                monitorY = GetSystemMetrics(77);

                int primaryWidth = GetSystemMetrics(0);
                int primaryHeight = GetSystemMetrics(1);

                widthMult = primaryWidth / ((float)screenWidth);
                heightMult = primaryHeight / ((float)screenHeight);
                return;
            }

            IntPtr hMonitor = MonitorFromWindow(_currentWindow, 2);
            MonitorInfo mi = new()
            {
                cbSize = Marshal.SizeOf<MonitorInfo>()
            };

            if (GetMonitorInfo(hMonitor, ref mi))
            {
                screenWidth = mi.rcMonitor.Right - mi.rcMonitor.Left;
                screenHeight = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
                monitorX = mi.rcMonitor.Left;
                monitorY = mi.rcMonitor.Top;
            }
            else
            {
                screenWidth = defaultScreenWidth;
                screenHeight = defaultScreenHeight;
                monitorX = 0;
                monitorY = 0;
            }
        }

        public void OnWindowFound()
        {
            const string LOG_IDENT = "WindowController::OnWindowFound";

            SaveWindow();

            _windowLong = GetWindowLong(_currentWindow, GWL_EXSTYLE);

            App.Logger.WriteLine(LOG_IDENT, $"Monitor X:{monitorX} Y:{monitorY} W:{screenWidth} H:{screenHeight}");
            App.Logger.WriteLine(LOG_IDENT, $"Window X:{_lastX} Y:{_lastY} W:{_lastWidth} H:{_lastHeight}");

            if (App.Settings.Prop.WindowAllowAll || IsGameAllowed())
            {
                _enabled = true;
                _activityWatcher.delay = _activityWatcher.windowLogDelay;
            }

            appTheme = ThemeEx.GetFinal(App.Settings.Prop.Theme);
            if (App.Settings.Prop.CanGameChangeColor && appTheme == Theme.Dark)
            {
                DisableWindowDarkMode();
                _lastWindowCaptionColor = Convert.ToUInt32("1F1F1F", 16);
                _ = DwmSetWindowAttribute(_currentWindow, 35, ref _lastWindowCaptionColor, sizeof(uint));
            }
        }

        public void StopWindow()
        {
            _activityWatcher.delay = 250;
            ResetWindow();
        }

        public void SaveWindow()
        {
            WindowRect winRect = new();
            _ = GetWindowRect(_currentWindow, ref winRect);

            _lastX = winRect.Left;
            _lastY = winRect.Top;
            _lastWidth = winRect.Right - winRect.Left;
            _lastHeight = winRect.Bottom - winRect.Top;

            _startingX = _lastX;
            _startingY = _lastY;
            _startingWidth = _lastWidth;
            _startingHeight = _lastHeight;

            UpdateWinMonitor();
        }

        public void ResetWindow()
        {
            if (changedWindow)
            {
                _lastX = _startingX;
                _lastY = _startingY;
                _lastWidth = _startingWidth;
                _lastHeight = _startingHeight;

                _lastTransparency = 1;
                _lastWindowColor = 0x000000;
                _lastTransparencyMode = LWA_COLORKEY;

                _ = MoveWindow(_currentWindow, _startingX, _startingY, _startingWidth, _startingHeight, false);
                _ = SetWindowLong(_currentWindow, GWL_EXSTYLE, _windowLong);
                SetBorderless(false);

                changedWindow = false;
            }

            _ = SendMessage(_currentWindow, WM_SETTEXT, IntPtr.Zero, "Roblox");

            if (App.Settings.Prop.CanGameChangeColor)
            {
                DisableWindowDarkMode();
                _lastWindowCaptionColor = Convert.ToUInt32(appTheme == Theme.Dark ? "1F1F1F" : "FFFFFF", 16);
                _ = DwmSetWindowAttribute(_currentWindow, 35, ref _lastWindowCaptionColor, sizeof(uint));

                _lastWindowBorderColor = Convert.ToUInt32("1F1F1F", 16);
                _ = DwmSetWindowAttribute(_currentWindow, 34, ref _lastWindowBorderColor, sizeof(uint));
            }
        }

        private void DisableWindowDarkMode()
        {
            uint disableDarkMode = 0;
            int cbAttribute = sizeof(uint);
            if (S_OK != DwmSetWindowAttribute(_currentWindow, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref disableDarkMode, cbAttribute))
            {
                _ = DwmSetWindowAttribute(_currentWindow, DWMWA_USE_IMMERSIVE_DARK_MODE, ref disableDarkMode, cbAttribute);
            }
            _ = UpdateWindow(_currentWindow);
        }

        public void OnMessage(Message message)
        {
            const string LOG_IDENT = "WindowController::OnMessage";

            if (!_foundWindow)
            {
                _currentWindow = FindWindow();
                _foundWindow = _currentWindow != IntPtr.Zero;

                if (_foundWindow) { OnWindowFound(); }
            }

            if (_currentWindow == IntPtr.Zero) { return; }

            if (!curUniverseAllowed && message.Command != "RequestWindowPermission" && message.Command != "StartWindow" && message.Command != "SetWindowTitle") { return; }
            if (!_enabled && message.Command != "RequestWindowPermission" && message.Command != "SetWindowTitle" && message.Command != "StartWindow") { return; }

            switch (message.Command)
            {
                case "RequestWindowPermission":
                    {
                        RequestPermission();
                        break;
                    }
                case "StartWindow":
                    {
                        if (_enabled) { return; }

                        UpdateState(true);
                        _activityWatcher.delay = _activityWatcher.windowLogDelay;
                        SaveWindow();
                        break;
                    }
                case "StopWindow":
                    {
                        if (!_enabled) { return; }

                        UpdateState(false);
                        break;
                    }
                case "ResetWindow":
                    _lastX = _startingX;
                    _lastY = _startingY;
                    _lastWidth = _startingWidth;
                    _lastHeight = _startingHeight;

                    _ = MoveWindow(_currentWindow, _startingX, _startingY, _startingWidth, _startingHeight, false);
                    break;
                case "SetWindow":
                    {
                        if (!App.Settings.Prop.MoveWindowAllowed) { break; }

                        WindowMessage? windowData = Deserialize<WindowMessage>(message);

                        if (windowData is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        if (windowData.Reset == true)
                        {
                            ResetWindow();
                            return;
                        }

                        if (windowData.ScaleWidth != null)
                            _lastSCWidth = (int)windowData.ScaleWidth;

                        if (windowData.ScaleHeight != null)
                            _lastSCHeight = (int)windowData.ScaleHeight;

                        float scaleX = ((float)screenWidth) / _lastSCWidth;
                        float scaleY = ((float)screenHeight) / _lastSCHeight;

                        if (windowData.Width != null)
                            _lastWidth = (int)(windowData.Width * scaleX);

                        if (windowData.Height != null)
                            _lastHeight = (int)(windowData.Height * scaleY);

                        if (windowData.X != null)
                        {
                            var fakeWidthFix = (_lastWidth - _lastWidth * widthMult) / 2;
                            _lastX = (int)(windowData.X * scaleX + fakeWidthFix);
                        }

                        if (windowData.Y != null)
                        {
                            var fakeHeightFix = (_lastHeight - _lastHeight * heightMult) / 2;
                            _lastY = (int)(windowData.Y * scaleY + fakeHeightFix);
                        }

                        changedWindow = true;
                        _ = MoveWindow(_currentWindow, _lastX + monitorX, _lastY + monitorY, (int)(_lastWidth * widthMult), (int)(_lastHeight * heightMult), false);
                        break;
                    }
                case "SetWindowTitle":
                    {
                        if (!App.Settings.Prop.TitleControlAllowed) { return; }

                        string? title = Deserialize<string>(message) ?? "Roblox";

                        _ = SendMessage(_currentWindow, WM_SETTEXT, IntPtr.Zero, title);
                        break;
                    }
                case "SetWindowTransparency":
                    {
                        if (!App.Settings.Prop.WindowTransparencyAllowed) { return; }
                        WindowTransparency? windowData = Deserialize<WindowTransparency>(message);

                        if (windowData is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        if (windowData.Transparency != null)
                            _lastTransparency = (byte)(windowData.Transparency * 255);

                        if (windowData.Color != null)
                            _lastWindowColor = Convert.ToUInt32(windowData.Color, 16);

                        if (windowData.UseAlpha != null)
                            _lastTransparencyMode = (windowData.UseAlpha == true) ? LWA_ALPHA : LWA_COLORKEY;

                        changedWindow = true;

                        if (_lastTransparency == 255)
                            _ = SetWindowLong(_currentWindow, GWL_EXSTYLE, _windowLong);
                        else
                        {
                            _ = SetWindowLong(_currentWindow, GWL_EXSTYLE, (_windowLong | WS_EX_LAYERED) & ~WS_EX_TRANSPARENT);
                            _ = SetLayeredWindowAttributes(_currentWindow, _lastWindowColor, _lastTransparency, _lastTransparencyMode);
                        }

                        break;
                    }
                case "SetWindowBorderless":
                    {
                        if (!App.Settings.Prop.MoveWindowAllowed) { break; }
                        WindowBorderless? windowData = Deserialize<WindowBorderless>(message);

                        if (windowData is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        SetBorderless(windowData.Enabled ?? false);
                        changedWindow = true;

                        break;
                    }
                case "SendNotification":
                    {
                        WindowNotification? notifData = Deserialize<WindowNotification>(message);

                        if (notifData is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        _activityWatcher.watcher._notifyIcon?.ShowAlert(notifData.Title ?? "[[MISSING TITLE]]", notifData.Caption ?? "[[MISSING CAPTION]]", notifData.Duration ?? 5);
                        break;
                    }
                case "SetWindowColor":
                    {
                        if (!App.Settings.Prop.CanGameChangeColor) { return; }
                        WindowColor? windowData = Deserialize<WindowColor>(message);

                        if (windowData is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        if (windowData.Reset == true)
                        {
                            windowData.Caption = appTheme == Theme.Dark ? "1F1F1F" : "FFFFFF";
                            windowData.Border = "1F1F1F";
                            windowData.Reset = false;
                        }

                        DisableWindowDarkMode();

                        if (windowData.Caption is not null)
                        {
                            _lastWindowCaptionColor = Convert.ToUInt32(windowData.Caption, 16);
                            _ = DwmSetWindowAttribute(_currentWindow, 35, ref _lastWindowCaptionColor, sizeof(uint));
                        }

                        if (windowData.Border is not null)
                        {
                            _lastWindowBorderColor = Convert.ToUInt32(windowData.Border, 16);
                            _ = DwmSetWindowAttribute(_currentWindow, 34, ref _lastWindowBorderColor, sizeof(uint));
                        }

                        break;
                    }
                default:
                    {
                        return;
                    }
            }
        }

        public void Dispose()
        {
            StopWindow();

            if (Watcher.RobloxPath != null)
            {
                var idsPath = Path.Combine(Watcher.RobloxPath, "content\\bloxstrap");
                if (Directory.Exists(idsPath))
                {
                    Directory.Delete(idsPath, true);
                }
            }

            GC.SuppressFinalize(this);
        }

        private static IntPtr FindWindow(string title = "Roblox")
        {
            try
            {
                Process? processById = Watcher.ProcessId != null ? Process.GetProcessById((int)Watcher.ProcessId) : null;
                if (processById != null)
                    return processById.MainWindowHandle;
            }
            catch { }

            Process[] tempProcesses = Process.GetProcesses();
            foreach (Process proc in tempProcesses)
                if (proc.MainWindowTitle == title)
                    return proc.MainWindowHandle;

            return IntPtr.Zero;
        }

        private static T? Deserialize<T>(Message message)
        {
            try
            {
                return message.Data.Deserialize<T>();
            }
            catch
            {
                return default;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref WindowRect rectangle);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);

        [DllImport("user32.dll")]
        static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hWnd, int dwAttribute, ref uint pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int cbSize;
            public WindowRect rcMonitor;
            public WindowRect rcWork;
            public uint dwFlags;
        }
    }
}