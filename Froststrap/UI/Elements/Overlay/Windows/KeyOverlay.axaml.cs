// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

using Froststrap.Integrations.Overlay;
using Froststrap.Integrations.OverlayModules;
using Froststrap.Models.Overlay;
using Froststrap.UI.ViewModels.Overlay;

namespace Froststrap.UI.Elements.Overlay
{
    internal partial class KeyOverlay : Window, IDisposable
    {
        private const int HotkeyId = 9002;
        private const int MoveHotkeyId = 9003;

        private const double ScreenMargin = 24;

        private static readonly PixelPoint Offscreen = new(-32000, -32000);
        private static readonly Cursor MoveCursor = new(StandardCursorType.SizeAll);

        private readonly KeyOverlayViewModel _viewModel;
        private readonly DispatcherTimer _timer;

        private IOverlayHost? _host;
        private IKeyOverlayInput? _input;

        private OverlayBounds? _lastBounds;
        private double _lastScaling = 1;

        private bool _moveMode;
        private bool _dragging;
        private PixelPoint _dragPointer;
        private int _dragLeft;
        private int _dragTop;

        private bool _prepared;
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

            if (host is null)
                return;

            _input = host.CreateKeyInput(App.Settings.Prop.SoftKeyProfile);

            Opacity = 0;
            Position = Offscreen;
            Show();
            Position = Offscreen;

            host.PrepareKeyOverlayWindow(this);

            host.BoundsChanged += OnOverlayBoundsChanged;
            host.GameVisibilityChanged += OnGameVisibilityChanged;
            host.WindowClosed += OnWindowClosed;
            host.HotkeyPressed += OnHotkeyPressed;

            RegisterHotkeys();

            KeyOverlayHotkey.Changed += OnHotkeyChanged;
            KeyOverlayHotkey.SuspendedChanged += OnHotkeySuspended;
            KeyOverlayMoveHotkey.Changed += OnMoveHotkeyChanged;
            KeyOverlayMoveHotkey.SuspendedChanged += OnMoveHotkeySuspended;

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

        private void SetMoveMode(bool enabled)
        {
            if (_moveMode == enabled || _disposed)
                return;

            _moveMode = enabled;
            _dragging = false;

            _viewModel.IsMoveMode = enabled;
            Cursor = enabled ? MoveCursor : null;

            _host?.SetClickThrough(this, !enabled);
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

        private void UpdateRegion()
        {
            if (!IsVisible || _disposed)
                return;

            if (_lastBounds is not OverlayBounds bounds)
                return;

            if (bounds.Rect.Width <= 0 || bounds.Rect.Height <= 0)
                return;

            int width = (int)Math.Round(Bounds.Width * _lastScaling);
            int height = (int)Math.Round(Bounds.Height * _lastScaling);

            if (width <= 0 || height <= 0)
                return;

            PixelPoint position = Position;

            var clip = new OverlayRegion(
                Math.Max((int)Math.Round(bounds.Rect.Left) - position.X, 0),
                Math.Max((int)Math.Round(bounds.Rect.Top) - position.Y, 0),
                Math.Min((int)Math.Round(bounds.Rect.Right) - position.X, width),
                Math.Min((int)Math.Round(bounds.Rect.Bottom) - position.Y, height));

            _host?.ClipOverlayToGame(this, clip);
        }

        private void OnGameVisibilityChanged(object? sender, bool visible)
        {
            _gameVisible = visible;
            UpdateVisibility();
        }

        private void OnWindowClosed(object? sender, EventArgs e) => SafeClose();

        private void OnHotkeyPressed(object? sender, int id)
        {
            if (id == HotkeyId)
                KeyOverlayService.Toggle();
            else if (id == MoveHotkeyId)
                SetMoveMode(!_moveMode);
        }

        private void OnHotkeyChanged(object? sender, EventArgs e) => RegisterHotkeys();

        private void OnHotkeySuspended(object? sender, bool suspended)
        {
            if (suspended)
                _host?.UnregisterHotkey(this, HotkeyId);
            else
                RegisterHotkeys();
        }

        private void OnMoveHotkeyChanged(object? sender, EventArgs e) => RegisterHotkeys();

        private void OnMoveHotkeySuspended(object? sender, bool suspended)
        {
            if (suspended)
                _host?.UnregisterHotkey(this, MoveHotkeyId);
            else
                RegisterHotkeys();
        }

        private void RegisterHotkeys()
        {
            if (_host is null)
                return;

            _host.UnregisterHotkey(this, HotkeyId);
            _host.UnregisterHotkey(this, MoveHotkeyId);

            if (!KeyOverlayHotkey.IsSuspended)
            {
                _host.RegisterHotkey(
                    this,
                    HotkeyId,
                    App.Settings.Prop.KeyOverlayHotkeyModifiers,
                    App.Settings.Prop.KeyOverlayHotkeyKey,
                    KeyOverlayHotkey.DefaultModifiers,
                    KeyOverlayHotkey.DefaultKey,
                    "key overlay");
            }

            if (!KeyOverlayMoveHotkey.IsSuspended)
            {
                _host.RegisterHotkey(
                    this,
                    MoveHotkeyId,
                    App.Settings.Prop.KeyOverlayMoveHotkeyModifiers,
                    App.Settings.Prop.KeyOverlayMoveHotkeyKey,
                    KeyOverlayMoveHotkey.DefaultModifiers,
                    KeyOverlayMoveHotkey.DefaultKey,
                    "key overlay move");
            }
        }

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
                _host.HotkeyPressed -= OnHotkeyPressed;

                _host.UnregisterHotkey(this, HotkeyId);
                _host.UnregisterHotkey(this, MoveHotkeyId);
            }

            KeyOverlayHotkey.Changed -= OnHotkeyChanged;
            KeyOverlayHotkey.SuspendedChanged -= OnHotkeySuspended;
            KeyOverlayMoveHotkey.Changed -= OnMoveHotkeyChanged;
            KeyOverlayMoveHotkey.SuspendedChanged -= OnMoveHotkeySuspended;

            GC.SuppressFinalize(this);
        }
    }
}