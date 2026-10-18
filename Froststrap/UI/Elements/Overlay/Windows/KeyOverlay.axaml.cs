// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

using Froststrap.Integrations.Overlay;
using Froststrap.Integrations.OverlayModules;
using Froststrap.UI.ViewModels.Overlay;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Froststrap.UI.Elements.Overlay
{
    internal partial class KeyOverlay : Window, IDisposable
    {
        private const int HotkeyId = 9002;
        private const int MoveHotkeyId = 9003;
        private const int WM_HOTKEY = 0x0312;

        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_TRANSPARENT = 0x00000020;
        private const uint WS_EX_LAYERED = 0x00080000;
        private const uint WS_EX_NOACTIVATE = 0x08000000;

        private const double ScreenMargin = 24;

        private static readonly PixelPoint Offscreen = new(-32000, -32000);

        private static readonly Cursor MoveCursor = new(StandardCursorType.SizeAll);

        private readonly KeyOverlayViewModel _viewModel;
        private readonly DispatcherTimer _timer;

        private IOverlayHost? _host;
        private KeyOverlayInput? _input;

        private HWND _hwnd;
        private OverlayBounds? _lastBounds;

        private double _lastScaling = 1;

        private readonly List<Occluder> _occluders = [];
        private RectInt _lastClip;
        private Occluder[] _lastOccluders = [];
        private bool _hasRegionState;

        private bool _moveMode;
        private bool _dragging;
        private PixelPoint _dragPointer;
        private int _dragLeft;
        private int _dragTop;

        private bool _prepared;
        private bool _win32Initialised;
        private bool _gameVisible = true;
        private bool _sessionVisible = true;
        private bool _disposed;

        public KeyOverlay()
        {
            InitializeComponent();

            _viewModel = Hud.ViewModel;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _timer.Tick += OnTick;

            SizeChanged += OnWindowSizeChanged;

            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
        }

        public void Prepare(IOverlayHost? host)
        {
            if (_prepared)
                return;

            _prepared = true;
            _host = host;
            _sessionVisible = KeyOverlayService.SessionEnabled;

            if (!OperatingSystem.IsWindows())
                return;

            _input = new KeyOverlayInput(host?.ProcessId ?? 0, App.Settings.Prop.SoftKeyProfile);

            Opacity = 0;
            Position = Offscreen;
            Show();
            Position = Offscreen;

            InitialiseWin32();

            if (_host is not null)
            {
                _host.BoundsChanged += OnOverlayBoundsChanged;
                _host.GameVisibilityChanged += OnGameVisibilityChanged;
                _host.WindowClosed += OnWindowClosed;
            }

            Opacity = 1;

            UpdateVisibility();
        }

        public void SetSessionVisible(bool visible)
        {
            _sessionVisible = visible;
            UpdateVisibility();
        }

        public void SafeClose()
        {
            if (_disposed)
                return;

            Close();
        }

        private void UpdateVisibility()
        {
            if (!_prepared || _disposed)
                return;

            if (_sessionVisible && _gameVisible)
            {
                if (!IsVisible)
                    Show();

                UpdatePosition();
                _timer.Start();
            }
            else
            {
                _timer.Stop();

                if (IsVisible)
                    Hide();
            }
        }

        private void OnTick(object? sender, EventArgs e)
        {
            if (_input is not null)
                _viewModel.Apply(_input.GetSnapshot());

            UpdateRegion();
        }

        #region Positioning

        private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e) => UpdatePosition();

        private void OnOverlayBoundsChanged(object? sender, OverlayBounds bounds)
        {
            _lastBounds = bounds;
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (!_prepared || _disposed || !IsVisible)
                return;

            if (_lastBounds is not OverlayBounds bounds)
                return;

            if (bounds.Rect.Width <= 0 || bounds.Rect.Height <= 0)
                return;

            if (_host?.IsGameMinimised() == true)
                return;

            double scaling = GetBoundsScaling(bounds);
            double width = Bounds.Width * scaling;
            double height = Bounds.Height * scaling;

            if (width <= 0 || height <= 0)
                return;

            _lastScaling = scaling;

            PixelPoint origin = GetBasePosition(bounds, scaling, width, height);

            Position = new PixelPoint(
                origin.X + (int)Math.Round(App.State.Prop.KeystrokePosition.X),
                origin.Y + (int)Math.Round(App.State.Prop.KeystrokePosition.Y));

            UpdateRegion();
        }

        private static PixelPoint GetBasePosition(OverlayBounds bounds, double scaling, double width, double height) => new(
            (int)Math.Round(bounds.Rect.Left + (ScreenMargin * scaling)),
            (int)Math.Round(bounds.Rect.Bottom - height - (ScreenMargin * scaling)));

        private double GetBoundsScaling(OverlayBounds bounds)
        {
            PixelPoint center = new(
                (int)(bounds.Rect.Left + (bounds.Rect.Width / 2)),
                (int)(bounds.Rect.Top + (bounds.Rect.Height / 2)));

            var screen = Screens.ScreenFromPoint(center);

            if (screen is not null && screen.Scaling > 0)
                return screen.Scaling;

            return RenderScaling > 0 ? RenderScaling : 1;
        }

        #endregion

        #region Move mode

        private void SetMoveMode(bool enabled)
        {
            if (_moveMode == enabled || _disposed)
                return;

            _moveMode = enabled;
            _dragging = false;

            _viewModel.IsMoveMode = enabled;
            Cursor = enabled ? MoveCursor : null;

            ApplyInputTransparency();
        }

        private void ApplyInputTransparency()
        {
            if (_hwnd == HWND.Null)
                return;

            uint exStyle = (uint)PInvoke.GetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

            exStyle |= WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;

            if (_moveMode)
                exStyle &= ~WS_EX_TRANSPARENT;
            else
                exStyle |= WS_EX_TRANSPARENT;

            _ = PInvoke.SetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle);
        }

        private (uint Style, uint ExStyle) OnWindowStyles(uint style, uint exStyle)
        {
            uint result = exStyle | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;

            if (_moveMode)
                result &= ~WS_EX_TRANSPARENT;
            else
                result |= WS_EX_TRANSPARENT;

            return (style, result);
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!_moveMode || _dragging || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            _dragging = true;
            _dragPointer = this.PointToScreen(e.GetPosition(this));
            _dragLeft = Position.X;
            _dragTop = Position.Y;

            e.Pointer.Capture(this);
            e.Handled = true;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_dragging)
                return;

            PixelPoint pointer = this.PointToScreen(e.GetPosition(this));

            DragTo(_dragLeft + (pointer.X - _dragPointer.X), _dragTop + (pointer.Y - _dragPointer.Y));

            e.Handled = true;
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_dragging)
                return;

            _dragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;

            SavePosition();
            SetMoveMode(false);
        }

        private static void SavePosition()
        {
            _ = Task.Run(() =>
            {
                App.State.SaveSetting(nameof(State.KeystrokePosition));
            }, CancellationToken.None);
        }

        private void DragTo(int left, int top)
        {
            if (_lastBounds is not OverlayBounds bounds)
                return;

            double width = Bounds.Width * _lastScaling;
            double height = Bounds.Height * _lastScaling;

            int minLeft = (int)Math.Round(bounds.Rect.Left);
            int minTop = (int)Math.Round(bounds.Rect.Top);
            int maxLeft = Math.Max(minLeft, (int)Math.Round(bounds.Rect.Right - width));
            int maxTop = Math.Max(minTop, (int)Math.Round(bounds.Rect.Bottom - height));

            left = Math.Clamp(left, minLeft, maxLeft);
            top = Math.Clamp(top, minTop, maxTop);

            PixelPoint origin = GetBasePosition(bounds, _lastScaling, width, height);

            App.State.Prop.KeystrokePosition.X = left - origin.X;
            App.State.Prop.KeystrokePosition.Y = top - origin.Y;

            Position = new PixelPoint(left, top);

            UpdateRegion();
        }

        #endregion

        #region Window region

        private void UpdateRegion()
        {
            if (!OperatingSystem.IsWindows() || _hwnd == HWND.Null || !IsVisible || _disposed)
                return;

            if (_lastBounds is not OverlayBounds bounds || bounds.Rect.Width <= 0 || bounds.Rect.Height <= 0)
                return;

            int width = (int)Math.Round(Bounds.Width * _lastScaling);
            int height = (int)Math.Round(Bounds.Height * _lastScaling);

            if (width <= 0 || height <= 0)
                return;

            PixelPoint position = Position;

            var clip = new RectInt(
                Math.Max((int)Math.Round(bounds.Rect.Left) - position.X, 0),
                Math.Max((int)Math.Round(bounds.Rect.Top) - position.Y, 0),
                Math.Min((int)Math.Round(bounds.Rect.Right) - position.X, width),
                Math.Min((int)Math.Round(bounds.Rect.Bottom) - position.Y, height));

            _occluders.Clear();

            if (clip.Right > clip.Left && clip.Bottom > clip.Top && !CollectOccluders(_occluders, position, clip))
            {
                _occluders.Clear();

                if (TryGetForegroundOccluder(position, clip, out Occluder foreground))
                    _occluders.Add(foreground);
            }

            if (HasSameRegion(clip))
                return;

            _lastClip = clip;
            _lastOccluders = [.. _occluders];
            _hasRegionState = true;

            HRGN region = clip.Right > clip.Left && clip.Bottom > clip.Top
                ? PInvoke.CreateRectRgn(clip.Left, clip.Top, clip.Right, clip.Bottom)
                : PInvoke.CreateRectRgn(0, 0, 0, 0);

            foreach (Occluder occluder in _occluders)
            {
                HRGN shape = PInvoke.CreateRectRgn(0, 0, 0, 0);

                if (PInvoke.GetWindowRgn(occluder.Handle, shape) == 0)
                {
                    _ = PInvoke.DeleteObject(shape);

                    shape = PInvoke.CreateRectRgn(
                        occluder.Screen.Left - position.X,
                        occluder.Screen.Top - position.Y,
                        occluder.Screen.Right - position.X,
                        occluder.Screen.Bottom - position.Y);
                }
                else
                {
                    _ = PInvoke.OffsetRgn(
                        shape,
                        occluder.Screen.Left - position.X,
                        occluder.Screen.Top - position.Y);
                }

                _ = PInvoke.CombineRgn(region, region, shape, RGN_COMBINE_MODE.RGN_DIFF);
                _ = PInvoke.DeleteObject(shape);
            }

            if (PInvoke.SetWindowRgn(_hwnd, region, true) == 0)
            {
                _hasRegionState = false;
                _ = PInvoke.DeleteObject(region);
            }
        }

        private bool HasSameRegion(RectInt clip)
        {
            if (!_hasRegionState || _lastClip != clip || _lastOccluders.Length != _occluders.Count)
                return false;

            for (int i = 0; i < _occluders.Count; i++)
            {
                if (_lastOccluders[i] != _occluders[i])
                    return false;
            }

            return true;
        }

        private bool CollectOccluders(List<Occluder> results, PixelPoint position, RectInt clip)
        {
            if (_host is null || _host.ProcessId <= 0)
                return false;

            uint gameProcessId = (uint)_host.ProcessId;
            HWND hwnd = PInvoke.GetTopWindow(HWND.Null);
            int guard = 0;

            while (hwnd != HWND.Null && guard++ < 1024)
            {
                HWND next = PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_HWNDNEXT);

                if (hwnd != _hwnd)
                {
                    _ = PInvoke.GetWindowThreadProcessId(hwnd, out uint processId);

                    if (processId == gameProcessId)
                    {
                        if (PInvoke.IsWindowVisible(hwnd) && !PInvoke.IsIconic(hwnd))
                            return true;

                        hwnd = next;
                        continue;
                    }

                    if (processId == (uint)Environment.ProcessId)
                    {
                        hwnd = next;
                        continue;
                    }

                    if (IsOccluderWindow(hwnd) && TryGetWindowRect(hwnd, out RECT rect))
                    {
                        var candidate = new RectInt(
                            rect.left - position.X,
                            rect.top - position.Y,
                            rect.right - position.X,
                            rect.bottom - position.Y);

                        if (candidate.Right > clip.Left && candidate.Left < clip.Right
                            && candidate.Bottom > clip.Top && candidate.Top < clip.Bottom
                            && !IsCloaked(hwnd))
                        {
                            results.Add(new Occluder(hwnd, new RectInt(rect.left, rect.top, rect.right, rect.bottom)));
                        }
                    }
                }

                hwnd = next;
            }

            return false;
        }

        private static bool IsOccluderWindow(HWND hwnd)
        {
            if (!PInvoke.IsWindowVisible(hwnd) || PInvoke.IsIconic(hwnd) || IsShellWindow(hwnd))
                return false;

            uint exStyle = (uint)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

            return (exStyle & (WS_EX_TRANSPARENT | WS_EX_LAYERED)) == 0;
        }

        private static bool TryGetWindowRect(HWND hwnd, out RECT bounds)
        {
            unsafe
            {
                RECT rect = default;

                var result = PInvoke.DwmGetWindowAttribute(
                    hwnd,
                    DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
                    &rect,
                    (uint)sizeof(RECT));

                if (result.Failed && !PInvoke.GetWindowRect(hwnd, out rect))
                {
                    bounds = default;
                    return false;
                }

                bounds = rect;
            }

            return bounds.right > bounds.left && bounds.bottom > bounds.top;
        }

        private static bool IsCloaked(HWND hwnd)
        {
            int[] buffer = new int[1];

            unsafe
            {
                fixed (int* pointer = buffer)
                {
                    var result = PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, pointer, (uint)sizeof(int));

                    return result.Succeeded && buffer[0] != 0;
                }
            }
        }

        private bool TryGetForegroundOccluder(PixelPoint position, RectInt clip, out Occluder occluder)
        {
            occluder = default;

            HWND foreground = PInvoke.GetForegroundWindow();

            if (foreground == HWND.Null)
                return false;

            _ = PInvoke.GetWindowThreadProcessId(foreground, out uint processId);

            if ((_host is not null && processId == (uint)_host.ProcessId)
                || processId == (uint)Environment.ProcessId
                || !IsOccluderWindow(foreground)
                || !TryGetWindowRect(foreground, out RECT rect))
            {
                return false;
            }

            var candidate = new RectInt(
                rect.left - position.X,
                rect.top - position.Y,
                rect.right - position.X,
                rect.bottom - position.Y);

            if (candidate.Right <= clip.Left || candidate.Left >= clip.Right
                || candidate.Bottom <= clip.Top || candidate.Top >= clip.Bottom)
            {
                return false;
            }

            occluder = new Occluder(foreground, new RectInt(rect.left, rect.top, rect.right, rect.bottom));
            return true;
        }

        private static bool IsShellWindow(HWND hwnd)
        {
            string className = GetWindowClass(hwnd);

            return className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
        }

        private static string GetWindowClass(HWND hwnd)
        {
            Span<char> buffer = stackalloc char[64];

            unsafe
            {
                fixed (char* pointer = buffer)
                {
                    int length = PInvoke.GetClassName(hwnd, pointer, buffer.Length);

                    return length > 0 ? new string(pointer, 0, length) : string.Empty;
                }
            }
        }

        private readonly record struct RectInt(int Left, int Top, int Right, int Bottom);

        private readonly record struct Occluder(HWND Handle, RectInt Screen);

        #endregion

        #region Host events

        private void OnGameVisibilityChanged(object? sender, bool visible)
        {
            _gameVisible = visible;
            UpdateVisibility();
        }

        private void OnWindowClosed(object? sender, EventArgs e) => SafeClose();

        #endregion

        #region Hotkeys

        private void InitialiseWin32()
        {
            if (!OperatingSystem.IsWindows() || _win32Initialised)
                return;

            var platformHandle = TryGetPlatformHandle();

            if (platformHandle is null)
            {
                App.Logger.Warn("Key overlay has no platform handle, hotkeys not registered");
                return;
            }

            _hwnd = new HWND(platformHandle.Handle);

            Win32Properties.AddWndProcHookCallback(this, HwndHook);
            Win32Properties.AddWindowStylesCallback(this, OnWindowStyles);

            ApplyInputTransparency();

            RegisterHotkey();
            RegisterMoveHotkey();

            KeyOverlayHotkey.Changed += OnHotkeyChanged;
            KeyOverlayHotkey.SuspendedChanged += OnHotkeySuspended;
            KeyOverlayMoveHotkey.Changed += OnMoveHotkeyChanged;
            KeyOverlayMoveHotkey.SuspendedChanged += OnMoveHotkeySuspended;

            _win32Initialised = true;
        }

        private IntPtr HwndHook(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_HOTKEY)
                return IntPtr.Zero;

            int hotkeyId = wParam.ToInt32();

            if (hotkeyId == HotkeyId)
                KeyOverlayService.Toggle();
            else if (hotkeyId == MoveHotkeyId)
                SetMoveMode(!_moveMode);
            else
                return IntPtr.Zero;

            handled = true;

            return IntPtr.Zero;
        }

        private void OnHotkeyChanged(object? sender, EventArgs e) => RegisterHotkey();

        private void OnHotkeySuspended(object? sender, bool suspended)
        {
            if (suspended)
                OverlayHotkeyRegistration.Unregister(_hwnd, HotkeyId);
            else
                RegisterHotkey();
        }

        private void RegisterHotkey()
        {
            if (!OperatingSystem.IsWindows() || _hwnd == HWND.Null)
                return;

            OverlayHotkeyRegistration.Unregister(_hwnd, HotkeyId);

            if (KeyOverlayHotkey.IsSuspended)
                return;

            var (modifiers, key) = OverlayHotkeyRegistration.Resolve(
                App.Settings.Prop.KeyOverlayHotkeyModifiers,
                App.Settings.Prop.KeyOverlayHotkeyKey,
                KeyOverlayHotkey.DefaultModifiers,
                KeyOverlayHotkey.DefaultKey,
                "key overlay");

            OverlayHotkeyRegistration.Register(_hwnd, HotkeyId, modifiers, key, "Key overlay");
        }

        private void OnMoveHotkeyChanged(object? sender, EventArgs e) => RegisterMoveHotkey();

        private void OnMoveHotkeySuspended(object? sender, bool suspended)
        {
            if (suspended)
                OverlayHotkeyRegistration.Unregister(_hwnd, MoveHotkeyId);
            else
                RegisterMoveHotkey();
        }

        private void RegisterMoveHotkey()
        {
            if (!OperatingSystem.IsWindows() || _hwnd == HWND.Null)
                return;

            OverlayHotkeyRegistration.Unregister(_hwnd, MoveHotkeyId);

            if (KeyOverlayMoveHotkey.IsSuspended)
                return;

            var (modifiers, key) = OverlayHotkeyRegistration.Resolve(
                App.Settings.Prop.KeyOverlayMoveHotkeyModifiers,
                App.Settings.Prop.KeyOverlayMoveHotkeyKey,
                KeyOverlayMoveHotkey.DefaultModifiers,
                KeyOverlayMoveHotkey.DefaultKey,
                "key overlay move");

            OverlayHotkeyRegistration.Register(_hwnd, MoveHotkeyId, modifiers, key, "Key overlay move");
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            Dispose();

            base.OnClosed(e);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _timer.Stop();
            _timer.Tick -= OnTick;

            PointerPressed -= OnPointerPressed;
            PointerMoved -= OnPointerMoved;
            PointerReleased -= OnPointerReleased;

            _input?.Dispose();
            _input = null;

            if (_host is not null)
            {
                _host.BoundsChanged -= OnOverlayBoundsChanged;
                _host.GameVisibilityChanged -= OnGameVisibilityChanged;
                _host.WindowClosed -= OnWindowClosed;
            }

            KeyOverlayHotkey.Changed -= OnHotkeyChanged;
            KeyOverlayHotkey.SuspendedChanged -= OnHotkeySuspended;
            KeyOverlayMoveHotkey.Changed -= OnMoveHotkeyChanged;
            KeyOverlayMoveHotkey.SuspendedChanged -= OnMoveHotkeySuspended;

            if (_win32Initialised)
                Win32Properties.RemoveWindowStylesCallback(this, OnWindowStyles);

            OverlayHotkeyRegistration.Unregister(_hwnd, HotkeyId);
            OverlayHotkeyRegistration.Unregister(_hwnd, MoveHotkeyId);

            GC.SuppressFinalize(this);
        }
    }
}
