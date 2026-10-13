using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Win32.Input;
using Froststrap.Enums.Overlay;
using Froststrap.Integrations;
using Froststrap.Integrations.Overlay;
using Froststrap.Models.Overlay;
using Froststrap.UI.Elements.Overlay.Controls;
using Froststrap.UI.ViewModels.Overlay;
using System.Collections.Generic;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using WindowState = Avalonia.Controls.WindowState;

namespace Froststrap.UI.Elements.Overlay
{
    internal partial class GameOverlay : Window
    {
        private const int ToggleHotkeyId = 9000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WM_HOTKEY = 0x0312;

        private const double CascadeStep = 44;

        private static readonly PixelPoint Offscreen = new(-32000, -32000);

        private readonly GameOverlayViewModel _viewModel;
        private readonly IOverlayHost? _overlay;

        private HWND _hwnd;
        private bool _win32Initialised;
        private bool _hotkeyRegistered;

        private int _placed;
        private bool _presenting;
        private bool _panelsWired;
        private bool _preloading = true;
        private bool _warmScheduled;

        private OverlayBounds? _lastBounds;

        private string? _savedLayout;
        private string _regionSignature = String.Empty;

        private readonly Dictionary<OverlayPanel, IDisposable> _panelVisibilitySubs = [];

        private bool _pinnedMode;
        private IBrush? _scrimBrush;

        private DispatcherTimer? _regionTimer;
        private int _draggingPanels;

        private FriendActivity ChatWindow => (FriendActivity)MessagesPanel.PanelContent!;
        private BadgeTracker BadgeTracker => (BadgeTracker)BadgesPanel.PanelContent!;
        private ServerBrowser ServerBrowser => (ServerBrowser)ServersPanel.PanelContent!;
        private Notes NotesPad => (Notes)NotesPanel.PanelContent!;
        private GameHistory GameHistory => (GameHistory)HistoryPanel.PanelContent!;

        public GameOverlay(IOverlayHost? overlay)
        {
            _overlay = overlay;
            _viewModel = new GameOverlayViewModel(this, overlay);

            DataContext = _viewModel;

            InitializeComponent();

            _viewModel.PanelOpened += (_, panel) => Place(panel);

            Deactivated += OnDeactivated;

            _overlay?.BoundsChanged += OnOverlayBoundsChanged;

            ChatWindow.Attach(overlay);
            BadgeTracker.Attach(overlay?.ActivityWatcher);
            BadgeTracker.BadgeEarned += OnBadgeEarned;
            ServerBrowser.Attach(overlay?.ActivityWatcher);
            GameHistory.Attach(overlay?.ActivityWatcher);
        }

        private void OnBadgeEarned(object? sender, Badge badge)
        {
            App.Logger.Info($"Badge earned: {badge.Name} ({badge.RarityText})");
        }

        private void OnOverlayBoundsChanged(object? sender, OverlayBounds bounds)
        {
            _lastBounds = bounds;

            if (_preloading)
            {
                Position = Offscreen;

                if (!_warmScheduled)
                {
                    _warmScheduled = true;
                    Dispatcher.UIThread.Post(WarmUp, DispatcherPriority.Background);
                }

                return;
            }

            double scaling = RenderScaling > 0 ? RenderScaling : 1;
            Position = new PixelPoint((int)bounds.Rect.Left, (int)bounds.Rect.Top);
        }

        private OverlayPanel PanelFor(OverlayPanelKind kind) => kind switch
        {
            OverlayPanelKind.Badges => BadgesPanel,
            OverlayPanelKind.Servers => ServersPanel,
            OverlayPanelKind.Notes => NotesPanel,
            OverlayPanelKind.History => HistoryPanel,
            _ => MessagesPanel
        };

        private void WirePanelEvents()
        {
            if (_panelsWired)
                return;

            _panelsWired = true;

            foreach (OverlayPanelKind kind in Enum.GetValues<OverlayPanelKind>())
            {
                OverlayPanel panel = PanelFor(kind);
                panel.PinChanged += OnPanelPinChanged;
                panel.DragStateChanged += OnPanelDragStateChanged;
                _panelVisibilitySubs[panel] = panel.GetObservable(Visual.IsVisibleProperty)
                    .Subscribe(new AnonymousObserver<bool>(_ => OnPanelVisibilityChanged()));
            }
        }

        private void UnwirePanelEvents()
        {
            if (!_panelsWired)
                return;

            _panelsWired = false;

            foreach (OverlayPanelKind kind in Enum.GetValues<OverlayPanelKind>())
            {
                OverlayPanel panel = PanelFor(kind);
                panel.PinChanged -= OnPanelPinChanged;
                panel.DragStateChanged -= OnPanelDragStateChanged;
            }

            foreach (IDisposable sub in _panelVisibilitySubs.Values)
                sub.Dispose();

            _panelVisibilitySubs.Clear();
        }

        private void Place(OverlayPanelKind kind)
        {
            OverlayPanel panel = PanelFor(kind);

            if (panel.Tag is bool placed && placed)
            {
                panel.Raise();
                return;
            }

            panel.Tag = true;

            if (Restore(kind, panel))
                return;

            (double width, double height) = kind switch
            {
                OverlayPanelKind.Notes => (560d, 420d),
                OverlayPanelKind.Servers => (880d, 520d),
                OverlayPanelKind.History => (580d, 440d),
                _ => (720d, 500d)
            };

            double offset = CascadeStep * _placed++;

            panel.PlaceAt(24 + offset, 16 + offset, width, height);
        }

        private bool Restore(OverlayPanelKind kind, OverlayPanel panel)
        {
            if (!App.State.Prop.OverlayPanels.TryGetValue(kind.ToString(), out OverlayPanelLayout? saved))
                return false;

            if (saved.Width < OverlayPanel.MinPanelWidth || saved.Height < OverlayPanel.MinPanelHeight)
            {
                panel.IsPinned = saved.Pinned;
                return false;
            }

            double scaleX = Scale(PanelSurface.Bounds.Width, saved.SurfaceWidth);
            double scaleY = Scale(PanelSurface.Bounds.Height, saved.SurfaceHeight);

            panel.IsPinned = saved.Pinned;

            panel.PlaceAt(saved.Left * scaleX, saved.Top * scaleY, saved.Width, saved.Height);

            return true;
        }

        private static double Scale(double now, double then) => then > 0 && now > 0 ? now / then : 1;

        private void RestoreDepth()
        {
            var order = Enum.GetValues<OverlayPanelKind>()
                .Where(kind => PanelFor(kind).IsVisible)
                .Select(kind => (Kind: kind, Saved: Saved(kind)))
                .Where(x => x.Saved is not null)
                .OrderBy(x => x.Saved!.Depth);

            foreach (var (kind, _) in order)
                PanelFor(kind).Raise();
        }

        private static OverlayPanelLayout? Saved(OverlayPanelKind kind) =>
            App.State.Prop.OverlayPanels.TryGetValue(kind.ToString(), out OverlayPanelLayout? saved) ? saved : null;

        private Dictionary<string, OverlayPanelLayout> BuildLayout()
        {
            var panels = new Dictionary<string, OverlayPanelLayout>();

            foreach (OverlayPanelKind kind in Enum.GetValues<OverlayPanelKind>())
            {
                OverlayPanel panel = PanelFor(kind);
                string name = kind.ToString();

                if (panel.Tag is not bool placed || !placed)
                {
                    if (Saved(kind) is OverlayPanelLayout previous)
                    {
                        previous.Open = false;
                        previous.Pinned = panel.IsPinned;
                        panels[name] = previous;
                    }

                    continue;
                }

                panels[name] = new OverlayPanelLayout
                {
                    Open = panel.IsVisible,
                    Pinned = panel.IsPinned,
                    Left = panel.Position.X,
                    Top = panel.Position.Y,
                    Width = panel.PanelSize.Width,
                    Height = panel.PanelSize.Height,
                    Depth = panel.Depth,
                    SurfaceWidth = panel.SurfaceSize.Width,
                    SurfaceHeight = panel.SurfaceSize.Height
                };
            }

            return panels;
        }

        private void SaveLayout()
        {
            try
            {
                var panels = BuildLayout();

                string serialised = JsonSerializer.Serialize(panels);

                if (serialised == _savedLayout)
                    return;

                _savedLayout = serialised;

                if (File.Exists(App.State.FileLocation) && App.State.HasFileOnDiskChanged())
                    App.State.Load(false);

                App.State.Prop.OverlayPanels = panels;

                Task.Run(App.State.Save);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to remember the layout");
                App.Logger.Error(ex);
            }
        }

        public void PrepareHidden()
        {
            if (!OperatingSystem.IsWindows())
                return;

            Opacity = 0;
            Position = Offscreen;
            Width = 1280;
            Height = 720;

            Show();

            Position = Offscreen;

            InitialiseWin32();
        }

        public void Toggle()
        {
            if (_preloading)
            {
                App.Logger.Info("Toggle ignored - still preloading");
                return;
            }

            if (_pinnedMode)
            {
                App.Logger.Info("Toggle while pinned -> presenting full overlay");
                Present();
                return;
            }

            bool visible = IsVisible;
            bool inFront = IsInFront();
            bool minimised = _overlay?.IsGameMinimised() == true;

            var action = OverlayToggle.Decide(visible, inFront, minimised);

            App.Logger.Info($"visible={visible} inFront={inFront} minimised={minimised} -> {action}");

            switch (action)
            {
                case OverlayToggleAction.Hide:
                    Dismiss();
                    break;

                case OverlayToggleAction.Present:
                    Present();
                    break;
            }
        }

        public void Dismiss()
        {
            NotesPad.Flush();
            SaveLayout();

            if (_pinnedMode)
            {
                Hide();
                ExitPinnedMode();
                return;
            }

            if (AnyPinnedPanels())
            {
                EnterPinnedMode();
                return;
            }

            Hide();
        }

        private void ScrimClicked(object? sender, PointerPressedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, Scrim))
                return;

            Dismiss();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key != Key.Escape)
                return;

            Dismiss();

            e.Handled = true;
        }

        private bool IsInFront() =>
            PInvoke.GetForegroundWindow() == _hwnd || _overlay?.IsGameForeground() == true;

        private void OnDeactivated(object? sender, EventArgs e)
        {
            if (_presenting)
                return;

            if (_preloading)
                return;

            if (_pinnedMode)
            {
                RefreshPinned();
                return;
            }

            if (IsOwnProcessForeground())
                return;

            if (_overlay?.IsGameForeground() == true)
                return;

            App.Logger.Info("Focus left the game, hiding");

            Dismiss();
        }

        private static unsafe bool IsOwnProcessForeground()
        {
            uint processId = 0;

            _ = PInvoke.GetWindowThreadProcessId(PInvoke.GetForegroundWindow(), &processId);

            return processId == Environment.ProcessId;
        }

        private void Present()
        {
            _presenting = true;

            if (_preloading)
            {
                _preloading = false;

                if (_lastBounds is OverlayBounds bounds)
                {
                    double scaling = RenderScaling > 0 ? RenderScaling : 1;
                    Position = new PixelPoint((int)bounds.Rect.Left, (int)bounds.Rect.Top);
                }
            }

            if (Opacity < 1)
                Opacity = 1;

            if (_pinnedMode)
            {
                ExitPinnedMode();
            }

            if (_overlay?.IsGameForeground() == false)
                _overlay.FocusGame();

            if (!IsVisible)
                Show();

            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            Reanchor();

            Activate();

            Dispatcher.UIThread.Post(() => _ = _viewModel.SyncAccountAsync(), DispatcherPriority.Background);

            Dispatcher.UIThread.Post(() => _presenting = false, DispatcherPriority.ApplicationIdle);
        }

        public void Reanchor()
        {
            RefreshPinned();

            if (OperatingSystem.IsWindows())
                _overlay?.AnchorAboveGame((IntPtr)_hwnd);
        }

        public void RefreshPinned()
        {
            if (_presenting)
                return;

            if (_preloading)
                return;

            bool gameOrOverlayInFront = _overlay?.IsGameForeground() == true || IsOwnProcessForeground();
            bool shouldShow = AnyPinnedPanels()
                && gameOrOverlayInFront
                && _overlay?.IsGameMinimised() != true;

            if (shouldShow)
            {
                if (!IsVisible)
                    Show();
            }
            else if (_pinnedMode && IsVisible)
            {
                Hide();
            }
        }

        private void Window_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            foreach (OverlayPanel panel in PanelSurface.Children.OfType<OverlayPanel>())
                panel.Clamp();
        }

        private async void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            WirePanelEvents();

            foreach (OverlayPanelKind kind in Enum.GetValues<OverlayPanelKind>())
            {
                OverlayPanel panel = PanelFor(kind);

                if (panel.IsVisible)
                    Place(kind);
            }

            RestoreDepth();

            _savedLayout = JsonSerializer.Serialize(BuildLayout());

            await _viewModel.OnLoaded();
        }

        private async void WarmUp()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

            if (!_preloading)
                return;

            _preloading = false;

            if (_lastBounds is OverlayBounds bounds)
            {
                double scaling = RenderScaling > 0 ? RenderScaling : 1;
                Position = new PixelPoint((int)bounds.Rect.Left, (int)bounds.Rect.Top);
            }

            Hide();
            Opacity = 1;
        }

        private void InitialiseWin32()
        {
            if (!OperatingSystem.IsWindows() || _win32Initialised)
                return;

            var platformHandle = TryGetPlatformHandle();
            if (platformHandle is null)
                return;

            _hwnd = new HWND(platformHandle.Handle);

            Win32Properties.AddWndProcHookCallback(this, HwndHook);

            int exStyle = PInvoke.GetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            _ = PInvoke.SetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

            RegisterToggleHotkey();
            OverlayHotkey.Changed += OnHotkeySettingChanged;
            OverlayHotkey.SuspendedChanged += OnHotkeySuspended;

            _win32Initialised = true;
        }

        private IntPtr HwndHook(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_HOTKEY || wParam.ToInt32() != ToggleHotkeyId)
                return IntPtr.Zero;

            Toggle();

            handled = true;

            return IntPtr.Zero;
        }

        private void OnHotkeySettingChanged(object? sender, EventArgs e) => RegisterToggleHotkey();

        private void OnHotkeySuspended(object? sender, bool suspended)
        {
            if (suspended)
                UnregisterToggleHotkey();
            else
                RegisterToggleHotkey();
        }

        private void UnregisterToggleHotkey()
        {
            OverlayHotkeyRegistration.Unregister(_hwnd, ToggleHotkeyId);
            _hotkeyRegistered = false;
        }

        private void RegisterToggleHotkey()
        {
            if (!OperatingSystem.IsWindows() || _hwnd == HWND.Null)
                return;

            OverlayHotkeyRegistration.Unregister(_hwnd, ToggleHotkeyId);
            _hotkeyRegistered = false;

            if (OverlayHotkey.IsSuspended)
                return;

            var (modifiers, key) = OverlayHotkeyRegistration.Resolve(
                App.Settings.Prop.OverlayHotkeyModifiers,
                App.Settings.Prop.OverlayHotkeyKey,
                OverlayHotkey.DefaultModifiers,
                OverlayHotkey.DefaultKey,
                "overlay");

            _hotkeyRegistered = OverlayHotkeyRegistration.Register(_hwnd, ToggleHotkeyId, modifiers, key, "Overlay");
        }

        #region Window region (click-through)

        private void StartRegionTimer()
        {
            _regionTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _regionTimer.Tick -= OnRegionTimerTick;
            _regionTimer.Tick += OnRegionTimerTick;
            _regionTimer.Start();
        }

        private void StopRegionTimer() => _regionTimer?.Stop();

        private void OnRegionTimerTick(object? sender, EventArgs e)
        {
            if (_draggingPanels > 0)
                return;

            UpdateWindowRegion();
        }

        private void UpdateWindowRegion()
        {
            if (!OperatingSystem.IsWindows() || !_win32Initialised)
                return;

            if (!_pinnedMode)
            {
                if (_regionSignature.Length == 0)
                    return;

                _regionSignature = String.Empty;
                _ = PInvoke.SetWindowRgn(_hwnd, HRGN.Null, true);
                return;
            }

            var boxes = new List<(int L, int T, int R, int B)>();
            double scaling = RenderScaling > 0 ? RenderScaling : 1;

            foreach (var child in PanelSurface.Children)
            {
                if (child is not OverlayPanel { IsPinned: true, IsVisible: true } panel)
                    continue;

                Point? topLeft = panel.TranslatePoint(default, this);
                if (topLeft is null)
                    continue;

                int left = (int)Math.Round(topLeft.Value.X * scaling);
                int top = (int)Math.Round(topLeft.Value.Y * scaling);
                int right = (int)Math.Round((topLeft.Value.X + panel.Bounds.Width) * scaling);
                int bottom = (int)Math.Round((topLeft.Value.Y + panel.Bounds.Height) * scaling);

                if (right > left && bottom > top)
                    boxes.Add((left, top, right, bottom));
            }

            string signature = String.Join('|', boxes);

            if (signature == _regionSignature)
                return;

            _regionSignature = signature;

            if (boxes.Count == 0)
            {
                _ = PInvoke.SetWindowRgn(_hwnd, HRGN.Null, true);
                return;
            }

            HRGN combined = PInvoke.CreateRectRgn(boxes[0].L, boxes[0].T, boxes[0].R, boxes[0].B);

            for (int i = 1; i < boxes.Count; i++)
            {
                HRGN rect = PInvoke.CreateRectRgn(boxes[i].L, boxes[i].T, boxes[i].R, boxes[i].B);
                _ = PInvoke.CombineRgn(combined, combined, rect, RGN_COMBINE_MODE.RGN_OR);
                _ = PInvoke.DeleteObject(rect);
            }

            if (PInvoke.SetWindowRgn(_hwnd, combined, true) == 0)
            {
                _regionSignature = String.Empty;
                _ = PInvoke.DeleteObject(combined);
            }
        }

        private void OnPanelDragStateChanged(object? sender, bool dragging)
        {
            _draggingPanels += dragging ? 1 : -1;

            if (_draggingPanels < 0)
                _draggingPanels = 0;

            if (_draggingPanels > 0)
            {
                if (OperatingSystem.IsWindows() && _win32Initialised)
                    _ = PInvoke.SetWindowRgn(_hwnd, HRGN.Null, true);
            }
            else
            {
                _regionSignature = String.Empty;
                UpdateWindowRegion();
            }
        }

        #endregion

        #region Pinned mode

        private bool AnyPinnedPanels() =>
            Enum.GetValues<OverlayPanelKind>()
                .Any(kind =>
                {
                    OverlayPanel panel = PanelFor(kind);
                    return panel.IsPinned && panel.IsVisible;
                });

        private void EnterPinnedMode()
        {
            if (_pinnedMode)
                return;

            if (!AnyPinnedPanels())
                return;

            _pinnedMode = true;

            _scrimBrush ??= Scrim.Background;
            Scrim.Background = null;

            ChromeDock.IsVisible = false;

            foreach (OverlayPanelKind kind in Enum.GetValues<OverlayPanelKind>())
            {
                OverlayPanel panel = PanelFor(kind);

                if (panel.IsPinned)
                    continue;

                panel.Opacity = 0;
                panel.IsHitTestVisible = false;
            }

            UpdateWindowRegion();
            StartRegionTimer();
        }

        private void ExitPinnedMode()
        {
            if (!_pinnedMode)
                return;

            _pinnedMode = false;

            StopRegionTimer();

            _regionSignature = String.Empty;
            UpdateWindowRegion();

            Scrim.Background = _scrimBrush ?? new SolidColorBrush(Color.FromArgb(0xB3, 0, 0, 0));

            ChromeDock.IsVisible = true;

            foreach (OverlayPanelKind kind in Enum.GetValues<OverlayPanelKind>())
            {
                OverlayPanel panel = PanelFor(kind);

                panel.Opacity = 1;
                panel.IsHitTestVisible = true;
            }
        }

        private void OnPanelPinChanged(object? sender, EventArgs e)
        {
            if (sender is not OverlayPanel panel)
                return;

            SaveLayout();

            if (!_pinnedMode)
                return;

            if (!panel.IsPinned)
            {
                panel.Opacity = 0;
                panel.IsHitTestVisible = false;
            }

            if (!AnyPinnedPanels())
            {
                Hide();
                ExitPinnedMode();
                return;
            }

            UpdateWindowRegion();
        }

        private void OnPanelVisibilityChanged()
        {
            if (!_pinnedMode)
                return;

            if (AnyPinnedPanels())
            {
                UpdateWindowRegion();
                return;
            }

            Hide();
            ExitPinnedMode();
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            NotesPad.Flush();

            SaveLayout();

            UnwirePanelEvents();

            StopRegionTimer();

            _overlay?.BoundsChanged -= OnOverlayBoundsChanged;

            OverlayHotkey.Changed -= OnHotkeySettingChanged;
            OverlayHotkey.SuspendedChanged -= OnHotkeySuspended;

            UnregisterToggleHotkey();

            base.OnClosed(e);
        }
    }
}