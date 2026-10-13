// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Win32.Input;

using Froststrap.Integrations.Overlay;
using Froststrap.Integrations.OverlayModules;
using Froststrap.Models.Overlay;
using Froststrap.UI.ViewModels.Overlay;

using Windows.Win32;
using Windows.Win32.Foundation;

using WindowState = Avalonia.Controls.WindowState;

namespace Froststrap.UI.Elements.Overlay
{
    internal partial class OverlaySearchBar : Window, IDisposable
    {
        private const int HotkeyId = 9001;
        private const int WM_HOTKEY = 0x0312;

        private static readonly PixelPoint Offscreen = new(-32000, -32000);

        private readonly OverlaySearchBarViewModel _viewModel;

        private IOverlayHost? _host;

        private HWND _hwnd;
        private bool _prepared;
        private bool _preloading = true;
        private bool _warmScheduled;
        private bool _presenting;
        private bool _disposed;

        private OverlayBounds? _lastBounds;

        public bool IsPresented { get; private set; }

        public OverlaySearchBar()
        {
            _viewModel = new OverlaySearchBarViewModel();
            DataContext = _viewModel;

            InitializeComponent();

            _viewModel.CloseRequested += OnCloseRequested;
            _viewModel.FocusInputRequested += OnFocusInputRequested;

            Deactivated += OnDeactivated;
        }

        public void Prepare(IOverlayHost? host)
        {
            if (_prepared)
                return;

            _prepared = true;
            _host = host;

            OverlaySearchBarViewModel.Attach(host);
            _viewModel.Prewarm();

            Opacity = 0;
            Width = 1280;
            Height = 720;
            WindowStartupLocation = WindowStartupLocation.Manual;

            Show();

            Position = Offscreen;

            if (_host is not null)
            {
                _host.BoundsChanged += OnOverlayBoundsChanged;
                _host.GameVisibilityChanged += OnGameVisibilityChanged;
            }

            if (OverlaySearchBarService.GameBounds is OverlayBounds cached)
                _lastBounds = cached;

            InitialiseHotkey();
        }

        private void OnCloseRequested(object? sender, EventArgs e) => Dismiss();

        private void OnFocusInputRequested(object? sender, EventArgs e) =>
            Dispatcher.UIThread.Post(() =>
            {
                SearchInput.Focus();
                SearchInput.SelectAll();
            }, DispatcherPriority.Input);

        private void OnOverlayBoundsChanged(object? sender, OverlayBounds bounds)
        {
            _lastBounds = bounds;

            if (_preloading)
            {
                Position = Offscreen;

                if (!_warmScheduled)
                {
                    _warmScheduled = true;
                    Dispatcher.UIThread.Post(() => _ = WarmUpAsync(), DispatcherPriority.Background);
                }

                return;
            }

            ApplyBounds(bounds);
        }

        private void ApplyBounds(OverlayBounds bounds)
        {
            double scaling = RenderScaling > 0 ? RenderScaling : 1;

            Position = new PixelPoint((int)bounds.Rect.Left, (int)bounds.Rect.Top);
            Width = Math.Max(bounds.Rect.Width / scaling, 0);
            Height = Math.Max(bounds.Rect.Height / scaling, 0);
        }

        private async Task WarmUpAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

            if (!_preloading)
                return;

            _preloading = false;

            if (_lastBounds is OverlayBounds bounds)
                ApplyBounds(bounds);

            Hide();
            Opacity = 1;
        }

        public void Present()
        {
            if (!_prepared || _preloading)
                return;

            if (_host?.IsGameForeground() == false)
                _host.FocusGame();

            if (!IsVisible)
                Show();

            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            if (_lastBounds is OverlayBounds bounds)
                ApplyBounds(bounds);

            _presenting = true;
            IsPresented = true;
            Opacity = 1;

            Activate();

            Dispatcher.UIThread.Post(() =>
            {
                SearchInput.Focus();
                SearchInput.SelectAll();
            }, DispatcherPriority.Input);

            _viewModel.OnShown();

            Dispatcher.UIThread.Post(() => _presenting = false, DispatcherPriority.ApplicationIdle);
        }

        public void Dismiss()
        {
            if (!IsPresented)
                return;

            IsPresented = false;

            _viewModel.OnHidden();

            Hide();
        }

        private void OnDeactivated(object? sender, EventArgs e)
        {
            if (_presenting)
                return;

            if (_preloading)
                return;

            if (!IsPresented)
                return;

            Dismiss();
        }

        private void OnGameVisibilityChanged(object? sender, bool visible)
        {
            if (!visible)
                Dismiss();
        }

        #region Hotkey

        private void InitialiseHotkey()
        {
            if (!OperatingSystem.IsWindows())
                return;

            var platformHandle = TryGetPlatformHandle();
            if (platformHandle is null)
            {
                App.Logger.Warn("Search bar has no platform handle, hotkey not registered");
                return;
            }

            _hwnd = new HWND(platformHandle.Handle);

            Win32Properties.AddWndProcHookCallback(this, HwndHook);

            RegisterHotkey();
            OverlaySearchBarHotkey.Changed += OnHotkeyChanged;
        }

        private void OnHotkeyChanged(object? sender, EventArgs e) => RegisterHotkey();

        private void RegisterHotkey()
        {
            if (!OperatingSystem.IsWindows() || _hwnd == HWND.Null)
                return;

            OverlayHotkeyRegistration.Unregister(_hwnd, HotkeyId);

            if (!App.Settings.Prop.SearchBarEnabled)
                return;

            var (modifiers, key) = OverlayHotkeyRegistration.Resolve(
                App.Settings.Prop.SearchBarHotkeyModifiers,
                App.Settings.Prop.SearchBarHotkeyKey,
                OverlaySearchBarHotkey.DefaultModifiers,
                OverlaySearchBarHotkey.DefaultKey,
                "search bar");

            OverlayHotkeyRegistration.Register(_hwnd, HotkeyId, modifiers, key, "Search bar");
        }

        private IntPtr HwndHook(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_HOTKEY || wParam.ToInt32() != HotkeyId)
                return IntPtr.Zero;

            if (IsPresented)
                Dismiss();
            else
                Present();

            handled = true;

            return IntPtr.Zero;
        }

        #endregion

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            switch (e.Key)
            {
                case Key.Escape:
                    Dismiss();
                    e.Handled = true;
                    break;

                case Key.Up:
                    _viewModel.MoveSelection(-1);
                    e.Handled = true;
                    break;

                case Key.Down:
                    _viewModel.MoveSelection(1);
                    e.Handled = true;
                    break;

                case Key.Enter:
                    _viewModel.JoinSelected();
                    e.Handled = true;
                    break;
            }
        }

        private void ScrimClicked(object? sender, PointerPressedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, Scrim))
                return;

            Dismiss();
        }

        private void ResultDoubleTapped(object? sender, TappedEventArgs e)
        {
            _viewModel.JoinSelected();
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

            IsPresented = false;

            _viewModel.OnHidden();

            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.FocusInputRequested -= OnFocusInputRequested;

            _viewModel.Dispose();

            if (_host is not null)
            {
                _host.BoundsChanged -= OnOverlayBoundsChanged;
                _host.GameVisibilityChanged -= OnGameVisibilityChanged;
            }

            OverlaySearchBarHotkey.Changed -= OnHotkeyChanged;

            OverlayHotkeyRegistration.Unregister(_hwnd, HotkeyId);

            GC.SuppressFinalize(this);
        }
    }
}