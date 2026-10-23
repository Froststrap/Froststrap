// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

using Froststrap.Enums.Overlay;
using Froststrap.Integrations.Overlay;
using Froststrap.Models.Overlay;
using Froststrap.UI.Elements.Overlay.Controls;
using Froststrap.UI.Utility;
using Froststrap.UI.ViewModels.Overlay;

using WindowState = Avalonia.Controls.WindowState;

namespace Froststrap.UI.Elements.Overlay
{
    internal partial class GameOverlay : Window, IDisposable
    {
        private const int ToggleHotkeyId = 9000;

        private const double CascadeStep = 44;

        private static readonly PixelPoint Offscreen = new(-32000, -32000);

        private static readonly OverlayPanelKind[] PanelKinds = Enum.GetValues<OverlayPanelKind>();

        private readonly GameOverlayViewModel _viewModel;
        private readonly IOverlayHost? _overlay;

        private int _placed;
        private bool _presenting;
        private bool _panelsWired;
        private bool _preloading = true;
        private bool _warmScheduled;
        private bool _disposed;

        private OverlayBounds? _lastBounds;

        private string? _savedLayout;
        private string _regionSignature = String.Empty;

        private readonly Dictionary<OverlayPanel, IDisposable> _panelVisibilitySubs = [];

        private readonly Dictionary<OverlayPanel, Window> _pinnedWindows = [];
        private readonly Dictionary<OverlayPanel, PinnedPanelLayout> _detachedLayouts = [];

        private bool _pinnedMode;
        private IBrush? _scrimBrush;

        private readonly record struct PinnedPanelLayout(
            double Left,
            double Top,
            double Width,
            double Height,
            double SurfaceWidth,
            double SurfaceHeight);

        private bool SeparatePinned => _overlay?.UsesSeparatePinnedWindows == true;

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

            _viewModel.PanelOpened += OnPanelOpened;

            Deactivated += OnDeactivated;

            _overlay?.BoundsChanged += OnOverlayBoundsChanged;

            if (_overlay is not null)
                _overlay.HotkeyPressed += OnHotkeyPressed;

            ChatWindow.Attach(overlay);
            BadgeTracker.Attach(overlay?.ActivityWatcher);
            BadgeTracker.BadgeEarned += OnBadgeEarned;
            ServerBrowser.Attach(overlay?.ActivityWatcher);
            GameHistory.Attach(overlay?.ActivityWatcher);
        }

        private void OnPanelOpened(object? sender, OverlayPanelKind kind) => Place(kind);

        private void OnBadgeEarned(object? sender, Badge badge)
        {
            App.Logger.Info($"Badge earned: {badge.Name} ({badge.RarityText})");
        }

        private void OnHotkeyPressed(object? sender, int id)
        {
            if (id != ToggleHotkeyId)
                return;

            Toggle();
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
                    Dispatcher.UIThread.Post(() => _ = WarmUpAsync(), DispatcherPriority.Background);
                }

                return;
            }

            Position = new PixelPoint((int)bounds.Rect.Left, (int)bounds.Rect.Top);

            RepositionPinnedWindows();
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

            foreach (OverlayPanelKind kind in PanelKinds)
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

            foreach (OverlayPanelKind kind in PanelKinds)
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
            var order = PanelKinds
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

            foreach (OverlayPanelKind kind in PanelKinds)
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

                bool detached = _detachedLayouts.TryGetValue(panel, out PinnedPanelLayout layout);

                panels[name] = new OverlayPanelLayout
                {
                    Open = panel.IsVisible,
                    Pinned = panel.IsPinned,
                    Left = detached ? layout.Left : panel.Position.X,
                    Top = detached ? layout.Top : panel.Position.Y,
                    Width = detached ? layout.Width : panel.PanelSize.Width,
                    Height = detached ? layout.Height : panel.PanelSize.Height,
                    Depth = panel.Depth,
                    SurfaceWidth = detached ? layout.SurfaceWidth : panel.SurfaceSize.Width,
                    SurfaceHeight = detached ? layout.SurfaceHeight : panel.SurfaceSize.Height
                };
            }

            return panels;
        }

        private void SaveLayout()
        {
            try
            {
                var panels = BuildLayout();

                string serialised = JsonHelper.Serialize(panels);

                if (serialised == _savedLayout)
                    return;

                _savedLayout = serialised;

                App.State.Prop.OverlayPanels = panels;

                _ = Task.Run(() => App.State.SaveSetting(nameof(State.OverlayPanels)), CancellationToken.None)
                    .ContinueWith(
                        t => App.Logger.Error($"Failed to save the layout: {t.Exception?.GetBaseException().Message}"),
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted,
                        TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to remember the layout");
                App.Logger.Error(ex);
            }
        }

        public void PrepareHidden()
        {
            Opacity = 0;
            Position = Offscreen;
            Width = 1280;
            Height = 720;

            Show();

            Position = Offscreen;

            _overlay?.PrepareGameWindow(this);
            _overlay?.SetWindowCloaked(this, true);

            RegisterToggleHotkey();

            OverlayHotkey.Changed += OnHotkeySettingChanged;
            OverlayHotkey.SuspendedChanged += OnHotkeySuspended;
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
            _overlay?.IsWindowForeground(this) == true || _overlay?.IsGameForeground() == true;

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

            if (_overlay?.IsOwnProcessForeground() == true)
                return;

            if (_overlay?.IsGameForeground() == true)
                return;

            App.Logger.Info("Focus left the game, hiding");

            Dismiss();
        }

        private void Present()
        {
            _presenting = true;

            _preloading = false;

            if (_lastBounds is OverlayBounds bounds)
                Position = new PixelPoint((int)bounds.Rect.Left, (int)bounds.Rect.Top);

            if (Opacity < 1)
                Opacity = 1;

            if (_pinnedMode)
                ExitPinnedMode();

            if (_overlay?.IsGameForeground() == false)
                _overlay.FocusGame();

            _overlay?.SetWindowCloaked(this, false);

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
            _overlay?.RaiseOverlayWindow(this);
        }

        public void RefreshPinned()
        {
            if (_presenting)
                return;

            if (_preloading)
                return;

            bool gameOrOverlayInFront = _overlay?.IsGameForeground() == true || _overlay?.IsOwnProcessForeground() == true;
            bool shouldShow = AnyPinnedPanels()
                && gameOrOverlayInFront
                && _overlay?.IsGameMinimised() != true;

            if (SeparatePinned)
            {
                if (!_pinnedMode)
                    return;

                foreach (Window window in _pinnedWindows.Values)
                {
                    if (shouldShow)
                    {
                        if (!window.IsVisible)
                            window.Show();
                    }
                    else if (window.IsVisible)
                    {
                        window.Hide();
                    }
                }

                return;
            }

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
            UpdateChromeDockLayout();

            foreach (OverlayPanel panel in PanelSurface.Children.OfType<OverlayPanel>())
                panel.Clamp();
        }

        private void ChromeDockContent_SizeChanged(object? sender, SizeChangedEventArgs e)
            => UpdateChromeDockLayout();

        private void UpdateChromeDockLayout()
        {
            if (!IsLoaded || ChromeDockContent is null || ChromeDockScroller is null || ChromeDock is null)
                return;

            double contentWidth = ChromeDockContent.DesiredSize.Width;
            if (contentWidth <= 0)
                return;

            const double horizontalWindowMargin = 16;
            const double dockHorizontalPadding = 20;
            const double scrollbarHeight = 14;
            const double bottomGapWhenScrollable = 8;

            double maxDockWidth = Math.Max(1, Bounds.Width - horizontalWindowMargin);
            double naturalDockWidth = contentWidth + dockHorizontalPadding;
            bool needsHorizontalScroll = naturalDockWidth > maxDockWidth + 0.5;

            double dockWidth = Math.Min(naturalDockWidth, maxDockWidth);
            double dockHeight = needsHorizontalScroll ? 68 + scrollbarHeight : 68;

            ChromeDock.Width = dockWidth;
            ChromeDock.Height = dockHeight;
            ChromeDock.Margin = new Thickness(0, 0, 0, needsHorizontalScroll ? bottomGapWhenScrollable + 28 : 28);
            ChromeDockScroller.Width = Math.Max(1, dockWidth - dockHorizontalPadding);
            ChromeDockScroller.Height = dockHeight - 2;
            ChromeDockContent.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        }

        private void Window_Loaded(object? sender, RoutedEventArgs e) => _ = OnLoadedAsync();

        private async Task OnLoadedAsync()
        {
            UpdateChromeDockLayout();
            WirePanelEvents();

            foreach (OverlayPanelKind kind in PanelKinds)
            {
                OverlayPanel panel = PanelFor(kind);

                if (panel.IsVisible)
                    Place(kind);
            }

            RestoreDepth();

            _savedLayout = JsonHelper.Serialize(BuildLayout());

            await _viewModel.OnLoaded();
        }

        private async Task WarmUpAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

            if (_disposed || !_preloading)
                return;

            SizeForBounds();

            Opacity = 1;

            await Rendering.WaitForFrameAsync();

            if (_disposed || !_preloading)
                return;

            Hide();

            _overlay?.SetWindowCloaked(this, false);

            _preloading = false;
        }

        private void SizeForBounds()
        {
            if (_lastBounds is not OverlayBounds bounds)
                return;

            double scaling = GetBoundsScaling(bounds);

            Width = Math.Max(bounds.Rect.Width / scaling, 0);
            Height = Math.Max(bounds.Rect.Height / scaling, 0);

            Position = Offscreen;
        }

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
            _overlay?.UnregisterHotkey(this, ToggleHotkeyId);
        }

        private void RegisterToggleHotkey()
        {
            if (_overlay is null)
                return;

            _overlay.UnregisterHotkey(this, ToggleHotkeyId);

            if (OverlayHotkey.IsSuspended)
                return;

            _overlay.RegisterHotkey(
                this,
                ToggleHotkeyId,
                App.Settings.Prop.OverlayHotkeyModifiers,
                App.Settings.Prop.OverlayHotkeyKey,
                OverlayHotkey.DefaultModifiers,
                OverlayHotkey.DefaultKey,
                "overlay");
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

        private void ClearWindowRegion()
        {
            if (_regionSignature.Length == 0)
                return;

            _regionSignature = String.Empty;
            _overlay?.ClearOverlayRegion(this);
        }

        private void UpdateWindowRegion()
        {
            if (SeparatePinned)
                return;

            if (!_pinnedMode)
            {
                ClearWindowRegion();
                return;
            }

            var regions = new List<OverlayRegion>();
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
                    regions.Add(new OverlayRegion(left, top, right, bottom));
            }

            string signature = String.Join('|', regions);

            if (signature == _regionSignature)
                return;

            _regionSignature = signature;

            if (regions.Count == 0)
            {
                _overlay?.ClearOverlayRegion(this);
                return;
            }

            _overlay?.SetOverlayRegion(this, regions);
        }

        private void OnPanelDragStateChanged(object? sender, bool dragging)
        {
            if (SeparatePinned)
                return;

            _draggingPanels += dragging ? 1 : -1;

            if (_draggingPanels < 0)
                _draggingPanels = 0;

            if (_draggingPanels > 0)
            {
                _overlay?.ClearOverlayRegion(this);
            }
            else
            {
                _regionSignature = String.Empty;
                UpdateWindowRegion();
            }
        }

        #endregion

        #region Pinned mode

        private bool AnyPinnedPanels()
        {
            foreach (OverlayPanelKind kind in PanelKinds)
            {
                OverlayPanel panel = PanelFor(kind);

                if (panel.IsPinned && panel.IsVisible)
                    return true;
            }

            return false;
        }

        private void EnterPinnedMode()
        {
            if (_pinnedMode)
                return;

            if (!AnyPinnedPanels())
                return;

            if (SeparatePinned)
            {
                EnterSeparatePinnedMode();
                return;
            }

            _pinnedMode = true;

            _scrimBrush ??= Scrim.Background;
            Scrim.Background = null;

            ChromeDock.IsVisible = false;

            foreach (OverlayPanelKind kind in PanelKinds)
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

            if (SeparatePinned)
            {
                ExitSeparatePinnedMode();
                return;
            }

            _pinnedMode = false;

            StopRegionTimer();
            ClearWindowRegion();

            Scrim.Background = _scrimBrush ?? new SolidColorBrush(Color.FromArgb(0xB3, 0, 0, 0));

            ChromeDock.IsVisible = true;

            foreach (OverlayPanelKind kind in PanelKinds)
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

            if (SeparatePinned)
            {
                if (panel.IsPinned)
                    DetachPanel(panel);
                else
                {
                    RestoreDetachedPanel(panel);
                    panel.Opacity = 0;
                    panel.IsHitTestVisible = false;
                }

                if (!AnyPinnedPanels())
                {
                    Hide();
                    ExitPinnedMode();
                    return;
                }

                RefreshPinned();
                return;
            }

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

            if (SeparatePinned)
            {
                foreach (OverlayPanel panel in _pinnedWindows.Keys.ToList())
                {
                    if (!panel.IsVisible)
                        RestoreDetachedPanel(panel);
                }

                if (!AnyPinnedPanels())
                {
                    Hide();
                    ExitPinnedMode();
                    return;
                }

                RefreshPinned();
                return;
            }

            if (AnyPinnedPanels())
            {
                UpdateWindowRegion();
                return;
            }

            Hide();
            ExitPinnedMode();
        }

        #endregion

        #region Separate pinned windows

        private void EnterSeparatePinnedMode()
        {
            _pinnedMode = true;

            _scrimBrush ??= Scrim.Background;
            Scrim.Background = null;

            ChromeDock.IsVisible = false;

            foreach (OverlayPanelKind kind in PanelKinds)
            {
                OverlayPanel panel = PanelFor(kind);

                if (panel.IsPinned)
                {
                    DetachPanel(panel);
                    continue;
                }

                panel.Opacity = 0;
                panel.IsHitTestVisible = false;
            }

            Hide();
            RefreshPinned();
        }

        private void ExitSeparatePinnedMode()
        {
            _pinnedMode = false;

            foreach (OverlayPanel panel in _pinnedWindows.Keys.ToList())
                RestoreDetachedPanel(panel);

            Scrim.Background = _scrimBrush ?? new SolidColorBrush(Color.FromArgb(0xB3, 0, 0, 0));

            ChromeDock.IsVisible = true;

            foreach (OverlayPanelKind kind in PanelKinds)
            {
                OverlayPanel panel = PanelFor(kind);

                panel.Opacity = 1;
                panel.IsHitTestVisible = true;
            }
        }

        private void DetachPanel(OverlayPanel panel)
        {
            if (_pinnedWindows.ContainsKey(panel))
                return;

            double width = Math.Max(panel.PanelSize.Width, OverlayPanel.MinPanelWidth);
            double height = Math.Max(panel.PanelSize.Height, OverlayPanel.MinPanelHeight);

            var layout = new PinnedPanelLayout(
                panel.Position.X,
                panel.Position.Y,
                width,
                height,
                panel.SurfaceSize.Width,
                panel.SurfaceSize.Height);

            PanelSurface.Children.Remove(panel);

            _detachedLayouts[panel] = layout;

            panel.IsDetached = true;
            panel.Width = width;
            panel.Height = height;

            Canvas.SetLeft(panel, 0);
            Canvas.SetTop(panel, 0);

            var surface = new Canvas
            {
                Width = width,
                Height = height,
                Background = Brushes.Transparent,
                Children = { panel }
            };

            var window = new Window
            {
                Width = width,
                Height = height,
                Opacity = 0,
                WindowDecorations = WindowDecorations.None,
                CanResize = false,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                Background = Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Content = surface
            };

            SetPinnedWindowPosition(window, layout);

            window.Show();

            _overlay?.PreparePinnedPanelWindow(window);

            window.PositionChanged += OnPinnedWindowPositionChanged;
            window.Closed += OnPinnedWindowClosed;

            _pinnedWindows[panel] = window;

            panel.DetachedDragRequested += OnPanelDetachedDragRequested;
            panel.DetachedResizeRequested += OnPanelDetachedResizeRequested;

            window.Opacity = 1;
        }

        private void RestoreDetachedPanel(OverlayPanel panel, bool closeWindow = true)
        {
            if (!_pinnedWindows.Remove(panel, out Window? window))
                return;

            panel.IsDetached = false;

            UnsubscribePinnedWindow(window);

            panel.DetachedDragRequested -= OnPanelDetachedDragRequested;
            panel.DetachedResizeRequested -= OnPanelDetachedResizeRequested;

            if (window.Content is Canvas surface)
                surface.Children.Remove(panel);

            window.Content = null;

            if (closeWindow)
                window.Close();

            if (!_detachedLayouts.Remove(panel, out PinnedPanelLayout layout))
                return;

            double scaleX = Scale(PanelSurface.Bounds.Width, layout.SurfaceWidth);
            double scaleY = Scale(PanelSurface.Bounds.Height, layout.SurfaceHeight);

            panel.Width = layout.Width;
            panel.Height = layout.Height;

            PanelSurface.Children.Add(panel);

            Canvas.SetLeft(panel, layout.Left * scaleX);
            Canvas.SetTop(panel, layout.Top * scaleY);

            panel.Clamp();
            panel.Raise();
        }

        private void UnsubscribePinnedWindow(Window window)
        {
            window.PositionChanged -= OnPinnedWindowPositionChanged;
            window.Closed -= OnPinnedWindowClosed;
        }

        private void SetPinnedWindowPosition(Window window, PinnedPanelLayout layout)
        {
            if (_lastBounds is not OverlayBounds bounds)
                return;

            double scale = GetBoundsScaling(bounds);
            if (scale <= 0)
                scale = 1;

            window.Width = layout.Width;
            window.Height = layout.Height;
            window.Position = new PixelPoint(
                (int)Math.Round(bounds.Rect.Left + (layout.Left * scale)),
                (int)Math.Round(bounds.Rect.Top + (layout.Top * scale)));
        }

        private void RepositionPinnedWindows()
        {
            if (_pinnedWindows.Count == 0)
                return;

            foreach ((OverlayPanel panel, Window window) in _pinnedWindows)
            {
                if (_detachedLayouts.TryGetValue(panel, out PinnedPanelLayout layout))
                    SetPinnedWindowPosition(window, layout);
            }
        }

        private void OnPinnedWindowPositionChanged(object? sender, PixelPointEventArgs e)
        {
            if (sender is not Window window)
                return;

            if (_lastBounds is not OverlayBounds bounds)
                return;

            OverlayPanel? owner = null;

            foreach ((OverlayPanel panel, Window w) in _pinnedWindows)
            {
                if (ReferenceEquals(w, window))
                {
                    owner = panel;
                    break;
                }
            }

            if (owner is null)
                return;

            if (!_detachedLayouts.TryGetValue(owner, out PinnedPanelLayout layout))
                return;

            double scale = GetBoundsScaling(bounds);
            if (scale <= 0)
                scale = 1;

            double localLeft = (window.Position.X - bounds.Rect.Left) / scale;
            double localTop = (window.Position.Y - bounds.Rect.Top) / scale;

            _detachedLayouts[owner] = layout with { Left = localLeft, Top = localTop };
        }

        private void OnPinnedWindowClosed(object? sender, EventArgs e)
        {
            if (sender is not Window window)
                return;

            OverlayPanel? owner = null;

            foreach ((OverlayPanel panel, Window w) in _pinnedWindows)
            {
                if (ReferenceEquals(w, window))
                {
                    owner = panel;
                    break;
                }
            }

            if (owner is null)
                return;

            RestoreDetachedPanel(owner, closeWindow: false);

            if (!AnyPinnedPanels())
            {
                Hide();
                ExitPinnedMode();
            }
        }

        private void OnPanelDetachedDragRequested(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not OverlayPanel panel)
                return;

            if (!_pinnedWindows.TryGetValue(panel, out Window? window))
                return;

            window.BeginMoveDrag(e);
        }

        private void OnPanelDetachedResizeRequested(object? sender, (string Edge, Vector Delta) args)
        {
            if (sender is not OverlayPanel panel)
                return;

            if (!_pinnedWindows.TryGetValue(panel, out Window? window))
                return;

            ApplyDetachedResize(panel, window, args.Edge, args.Delta);
        }

        private void ApplyDetachedResize(OverlayPanel panel, Window window, string edge, Vector delta)
        {
            double width = window.Width;
            double height = window.Height;
            double windowLeft = window.Position.X;
            double windowTop = window.Position.Y;

            if (edge.Contains("Left", StringComparison.Ordinal))
            {
                double applied = Grow(width, -delta.X, OverlayPanel.MinPanelWidth, out double newWidth);
                width = newWidth;
                windowLeft -= applied;
            }
            else if (edge.Contains("Right", StringComparison.Ordinal))
            {
                Grow(width, delta.X, OverlayPanel.MinPanelWidth, out double newWidth);
                width = newWidth;
            }

            if (edge.Contains("Top", StringComparison.Ordinal))
            {
                double applied = Grow(height, -delta.Y, OverlayPanel.MinPanelHeight, out double newHeight);
                height = newHeight;
                windowTop -= applied;
            }
            else if (edge.Contains("Bottom", StringComparison.Ordinal))
            {
                Grow(height, delta.Y, OverlayPanel.MinPanelHeight, out double newHeight);
                height = newHeight;
            }

            window.Width = width;
            window.Height = height;
            window.Position = new PixelPoint((int)Math.Round(windowLeft), (int)Math.Round(windowTop));

            if (window.Content is Canvas surface)
            {
                surface.Width = width;
                surface.Height = height;
            }

            panel.Width = width;
            panel.Height = height;

            if (_lastBounds is OverlayBounds bounds && _detachedLayouts.TryGetValue(panel, out PinnedPanelLayout layout))
            {
                double scale = GetBoundsScaling(bounds);
                if (scale <= 0)
                    scale = 1;

                _detachedLayouts[panel] = layout with
                {
                    Left = (windowLeft - bounds.Rect.Left) / scale,
                    Top = (windowTop - bounds.Rect.Top) / scale,
                    Width = width,
                    Height = height
                };
            }
        }

        private static double Grow(double current, double delta, double min, out double result)
        {
            result = Math.Max(current + delta, min);
            return result - current;
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

            NotesPad.Flush();

            SaveLayout();

            if (SeparatePinned)
                ExitSeparatePinnedMode();

            UnwirePanelEvents();

            StopRegionTimer();

            _overlay?.BoundsChanged -= OnOverlayBoundsChanged;

            if (_overlay is not null)
                _overlay.HotkeyPressed -= OnHotkeyPressed;

            OverlayHotkey.Changed -= OnHotkeySettingChanged;
            OverlayHotkey.SuspendedChanged -= OnHotkeySuspended;

            UnregisterToggleHotkey();

            _viewModel.PanelOpened -= OnPanelOpened;
            _viewModel.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}