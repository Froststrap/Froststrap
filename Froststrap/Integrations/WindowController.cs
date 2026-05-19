using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Windows.Win32.Foundation;
using Message = Froststrap.Models.BloxstrapRPC.Message;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.Graphics.Dwm;

namespace Froststrap.Integrations
{
    public class WindowController : IDisposable
    {
        private readonly ActivityWatcher _activityWatcher;
        private UI.Elements.ContextMenu.MenuContainer? _menuContainer;
        private IntPtr _currentWindow;
        private int _windowLong = 0x00000000;
        private bool _foundWindow = false;
        private bool enabled = false;

        public const uint WM_SETTEXT = 0x000C;
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const uint LWA_COLORKEY = 0x00000001;
        public const uint LWA_ALPHA = 0x00000002;
        public const int WS_BORDER = 0x00800000;
        public const int WS_CAPTION = 0x00C00000;
        private const int GWL_STYLE = -16;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public void SetBorderless(bool borderless)
        {
            if (_currentWindow == IntPtr.Zero) return;

            int style = PInvoke.GetWindowLong((HWND)_currentWindow, WINDOW_LONG_PTR_INDEX.GWL_STYLE);

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

            _ = PInvoke.SetWindowLong((HWND)_currentWindow, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);
            _ = PInvoke.SetWindowPos((HWND)_currentWindow, HWND.Null, 0, 0, 0, 0, (SET_WINDOW_POS_FLAGS)(SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED));
        }

        private const int defaultScreenWidth = 1280;
        private const int defaultScreenHeight = 720;

        public int monitorX = 0;
        public int monitorY = 0;

        public float widthMult = 1;
        public float heightMult = 1;

        private int screenWidth = 0;
        private int screenHeight = 0;

        private bool changedWindow = false;

        private int _lastX = 0;
        private int _lastY = 0;
        private int _lastWidth = 0;
        private int _lastHeight = 0;
        private int _lastSCWidth = 0;
        private int _lastSCHeight = 0;
        private byte _lastTransparency = 1;
        private uint _lastWindowColor = 0x000000;
        private uint _lastWindowCaptionColor = 0x000000;
        private uint _lastWindowBorderColor = 0x000000;
        private uint _lastTransparencyMode = 0x00000001;

        private int _startingX = 0;
        private int _startingY = 0;
        private int _startingWidth = 0;
        private int _startingHeight = 0;

        private bool curUniverseAllowed = false;
        private long prevUniverse = 0;

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
                _ = Dispatcher.UIThread.InvokeAsync(() =>
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
                foreach (FileInfo file in directory.GetFiles())
                {
                    if (file.Name != "enabled.png") file.Delete();
                }
            }
            else
            {
                _ = Directory.CreateDirectory(idsPath);
            }

            var currentUniverse = _activityWatcher.Data.UniverseId;

            curUniverseAllowed = App.Settings.Prop.WindowAllowAll || IsGameAllowed(currentUniverse);
            if (!curUniverseAllowed) { return; }

            using var image = new Image<L8>(3, 1);
            image[0, 0] = new L8(App.Settings.Prop.MoveWindowAllowed ? (byte)255 : (byte)0);
            image[1, 0] = new L8(App.Settings.Prop.TitleControlAllowed ? (byte)255 : (byte)0);
            image[2, 0] = new L8(App.Settings.Prop.WindowTransparencyAllowed ? (byte)255 : (byte)0);

            string outputPath = Path.Combine(idsPath, $"{currentUniverse}.png");
            image.SaveAsPng(outputPath);
        }

        public bool IsGameAllowed(long universeId = -1)
        {
            if (universeId == -1) { universeId = _activityWatcher.Data.UniverseId; }
            return App.Settings.Prop.WindowAllowedUniverses.Contains(universeId);
        }

        public void UpdateState(bool state)
        {
            enabled = state;
            if (!enabled)
            {
                StopWindow();
            }
        }

        public void UpdateWinMonitor()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime classicDesktop)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var screens = classicDesktop.MainWindow?.Screens ?? new Window().Screens;

                    if (App.Settings.Prop.WindowMonitorStyle == WindowMonitorStyle.All)
                    {
                        var allScreens = screens.All;
                        if (allScreens is { Count: > 0 })
                        {
                            int minX = int.MaxValue, minY = int.MaxValue;
                            int maxX = int.MinValue, maxY = int.MinValue;

                            foreach (var screen in allScreens)
                            {
                                var b = screen.Bounds;
                                if (b.X < minX) minX = b.X;
                                if (b.Y < minY) minY = b.Y;
                                if (b.X + b.Width > maxX) maxX = b.X + b.Width;
                                if (b.Y + b.Height > maxY) maxY = b.Y + b.Height;
                            }

                            monitorX = minX;
                            monitorY = minY;
                            screenWidth = maxX - minX;
                            screenHeight = maxY - minY;
                        }

                        var primaryScreen = screens.Primary;
                        if (primaryScreen != null)
                        {
                            widthMult = primaryScreen.Bounds.Width / ((float)screenWidth);
                            heightMult = primaryScreen.Bounds.Height / ((float)screenHeight);
                        }
                        return;
                    }

                    var windowBounds = new PixelRect(_lastX, _lastY, _lastWidth, _lastHeight);
                    var curScreen = screens.ScreenFromBounds(windowBounds) ?? screens.Primary;

                    if (curScreen != null)
                    {
                        screenWidth = curScreen.Bounds.Width;
                        screenHeight = curScreen.Bounds.Height;
                        monitorX = curScreen.Bounds.X;
                        monitorY = curScreen.Bounds.Y;
                    }
                });
            }
        }

        public void OnWindowFound()
        {
            const string LOG_IDENT = "WindowController::onWindowFound";

            SaveWindow();

            _windowLong = PInvoke.GetWindowLong((HWND)_currentWindow, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

            App.Logger.WriteLine(LOG_IDENT, $"Monitor X:{monitorX} Y:{monitorY} W:{screenWidth} H:{screenHeight}");
            App.Logger.WriteLine(LOG_IDENT, $"Window X:{_lastX} Y:{_lastY} W:{_lastWidth} H:{_lastHeight}");

            appTheme = ThemeEx.GetFinal(App.Settings.Prop.Theme);
            if (App.Settings.Prop.CanGameChangeColor && appTheme == Theme.Dark)
            {
                DisableWindowDarkMode();
                _lastWindowCaptionColor = Convert.ToUInt32("1F1F1F", 16);

                unsafe
                {
                    uint colorAttr = _lastWindowCaptionColor;
                    _ = PInvoke.DwmSetWindowAttribute((HWND)_currentWindow, (DWMWINDOWATTRIBUTE)35, &colorAttr, sizeof(uint));
                }
            }
        }

        public void StopWindow()
        {
            _activityWatcher.delay = 250;
            ResetWindow();
        }

        public void SaveWindow()
        {
            _ = PInvoke.GetWindowRect((HWND)_currentWindow, out RECT winRect);

            _lastX = winRect.left;
            _lastY = winRect.top;
            _lastWidth = winRect.right - winRect.left;
            _lastHeight = winRect.bottom - winRect.top;

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

                _ = PInvoke.MoveWindow((HWND)_currentWindow, _startingX, _startingY, _startingWidth, _startingHeight, false);
                _ = PInvoke.SetWindowLong((HWND)_currentWindow, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, _windowLong);
                SetBorderless(false);

                changedWindow = false;
            }

            unsafe
            {
                fixed (char* pTitle = "Roblox")
                {
                    _ = PInvoke.SendMessage((HWND)_currentWindow, WM_SETTEXT, default, new LPARAM((nint)pTitle));
                }
            }

            if (App.Settings.Prop.CanGameChangeColor)
            {
                DisableWindowDarkMode();
                _lastWindowCaptionColor = Convert.ToUInt32(appTheme == Theme.Dark ? "1F1F1F" : "FFFFFF", 16);

                unsafe
                {
                    uint captionColor = _lastWindowCaptionColor;
                    _ = PInvoke.DwmSetWindowAttribute((HWND)_currentWindow, (DWMWINDOWATTRIBUTE)35, &captionColor, sizeof(uint));

                    _lastWindowBorderColor = Convert.ToUInt32("1F1F1F", 16);
                    uint borderColor = _lastWindowBorderColor;
                    _ = PInvoke.DwmSetWindowAttribute((HWND)_currentWindow, (DWMWINDOWATTRIBUTE)34, &borderColor, sizeof(uint));
                }
            }
        }

        void DisableWindowDarkMode()
        {
            uint disableDarkMode = 0;
            unsafe
            {
                if (S_OK != PInvoke.DwmSetWindowAttribute((HWND)_currentWindow, (DWMWINDOWATTRIBUTE)DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, &disableDarkMode, sizeof(uint)))
                {
                    _ = PInvoke.DwmSetWindowAttribute((HWND)_currentWindow, (DWMWINDOWATTRIBUTE)DWMWA_USE_IMMERSIVE_DARK_MODE, &disableDarkMode, sizeof(uint));
                }
            }
            _ = PInvoke.UpdateWindow((HWND)_currentWindow);
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

            if (!curUniverseAllowed && (message.Command != "RequestWindowPermission" || prevUniverse == _activityWatcher.Data.UniverseId) && message.Command != "SetWindowTitle") { return; }
            if (!enabled && message.Command != "RequestWindowPermission" && message.Command != "SetWindowTitle" && message.Command != "StartWindow") { return; }

            switch (message.Command)
            {
                case "RequestWindowPermission":
                    {
                        RequestPermission();
                        break;
                    }
                case "StartWindow":
                    {
                        if (enabled) { return; }

                        UpdateState(true);
                        _activityWatcher.delay = _activityWatcher.windowLogDelay;
                        SaveWindow();
                        break;
                    }
                case "StopWindow":
                    {
                        if (!enabled) { return; }

                        UpdateState(false);
                        break;
                    }
                case "ResetWindow":
                    _lastX = _startingX;
                    _lastY = _startingY;
                    _lastWidth = _startingWidth;
                    _lastHeight = _startingHeight;

                    _ = PInvoke.MoveWindow((HWND)_currentWindow, _startingX, _startingY, _startingWidth, _startingHeight, false);
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
                        _ = PInvoke.MoveWindow((HWND)_currentWindow, _lastX + monitorX, _lastY + monitorY, (int)(_lastWidth * widthMult), (int)(_lastHeight * heightMult), false);
                        break;
                    }
                case "SetWindowTitle":
                    {
                        if (!App.Settings.Prop.TitleControlAllowed) { return; }

                        string? title = Deserialize<string>(message);

                        title ??= "Roblox";

                        unsafe
                        {
                            fixed (char* pTitle = title)
                            {
                                _ = PInvoke.SendMessage((HWND)_currentWindow, WM_SETTEXT, default, new LPARAM((nint)pTitle));
                            }
                        }
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
                            _ = PInvoke.SetWindowLong((HWND)_currentWindow, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, _windowLong);
                        else
                        {
                            _ = PInvoke.SetWindowLong((HWND)_currentWindow, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (_windowLong | WS_EX_LAYERED) & ~WS_EX_TRANSPARENT);
                            _ = PInvoke.SetLayeredWindowAttributes((HWND)_currentWindow, new COLORREF(_lastWindowColor), _lastTransparency, (LAYERED_WINDOW_ATTRIBUTES_FLAGS)_lastTransparencyMode);
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

                        _activityWatcher.watcher._notifyIcon?.ShowAlert(notifData.Title ?? "[[MISSING TITLE]]", notifData.Caption ?? "[[MISSING CAPTION]]", notifData.Duration ?? 5, Avalonia.Controls.Notifications.NotificationType.Information);
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

                        unsafe
                        {
                            if (windowData.Caption is not null)
                            {
                                _lastWindowCaptionColor = Convert.ToUInt32(windowData.Caption, 16);
                                uint captionColor = _lastWindowCaptionColor;
                                _ = PInvoke.DwmSetWindowAttribute((HWND)_currentWindow, (DWMWINDOWATTRIBUTE)35, &captionColor, sizeof(uint));
                            }

                            if (windowData.Border is not null)
                            {
                                _lastWindowBorderColor = Convert.ToUInt32(windowData.Border, 16);
                                uint borderColor = _lastWindowBorderColor;
                                _ = PInvoke.DwmSetWindowAttribute((HWND)_currentWindow, (DWMWINDOWATTRIBUTE)34, &borderColor, sizeof(uint));
                            }
                        }

                        break;
                    }
                default:
                    return;
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
            {
                if (proc.MainWindowTitle == title)
                    return proc.MainWindowHandle;
            }

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
    }
}