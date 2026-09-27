using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Win32.Input;
using Froststrap.Enums.Overlay;
using Froststrap.UI.Elements.Overlay.Controls;
using Froststrap.UI.ViewModels.Overlay;
using Windows.Win32;
using Windows.Win32.Foundation;
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

        private readonly GameOverlayViewModel _viewModel;
        private readonly Integrations.Overlay? _overlay;

        private HWND _hwnd;
        private bool _win32Initialised;
        private bool _hotkeyRegistered;

        private int _placed;
        private bool _presenting;

        private string? _savedLayout;

        private FriendActivity ChatWindow => (FriendActivity)MessagesPanel.PanelContent!;
        private BadgeTracker BadgeTracker => (BadgeTracker)BadgesPanel.PanelContent!;
        private ServerBrowser ServerBrowser => (ServerBrowser)ServersPanel.PanelContent!;
        private Notes NotesPad => (Notes)NotesPanel.PanelContent!;
        private GameBrowser GameBrowser => (GameBrowser)GamesPanel.PanelContent!;
        private GameHistory GameHistory => (GameHistory)HistoryPanel.PanelContent!;

        public GameOverlay(Integrations.Overlay? overlay)
        {
            try
            {
                if (!App.OverlayLayout.Loaded)
                    App.OverlayLayout.Load(false);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to read the saved layout");
                App.Logger.Error(ex);
            }

            _overlay = overlay;
            _viewModel = new GameOverlayViewModel(this, overlay);

            DataContext = _viewModel;

            InitializeComponent();

            _viewModel.PanelOpened += (_, panel) => Place(panel);

            Deactivated += OnDeactivated;

            ChatWindow.Attach(overlay?.Messaging.Party);
            BadgeTracker.Attach(overlay?.ActivityWatcher);
            ServerBrowser.Attach(overlay?.ActivityWatcher);
            GameBrowser.Attach(overlay?.ActivityWatcher);
            GameHistory.Attach(overlay?.ActivityWatcher);
        }

        private OverlayPanel PanelFor(OverlayPanelKind kind) => kind switch
        {
            OverlayPanelKind.Badges => BadgesPanel,
            OverlayPanelKind.Servers => ServersPanel,
            OverlayPanelKind.Notes => NotesPanel,
            OverlayPanelKind.Games => GamesPanel,
            OverlayPanelKind.History => HistoryPanel,
            _ => MessagesPanel
        };

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
                OverlayPanelKind.Games => (800d, 540d),
                OverlayPanelKind.History => (580d, 440d),
                _ => (720d, 500d)
            };

            double offset = CascadeStep * _placed++;

            panel.PlaceAt(24 + offset, 16 + offset, width, height);
        }

        private bool Restore(OverlayPanelKind kind, OverlayPanel panel)
        {
            if (!App.OverlayLayout.Prop.Panels.TryGetValue(kind.ToString(), out OverlayPanelLayout? saved))
                return false;

            if (saved.Width < OverlayPanel.MinPanelWidth || saved.Height < OverlayPanel.MinPanelHeight)
                return false;

            double scaleX = Scale(PanelSurface.Bounds.Width, saved.SurfaceWidth);
            double scaleY = Scale(PanelSurface.Bounds.Height, saved.SurfaceHeight);

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
            App.OverlayLayout.Prop.Panels.TryGetValue(kind.ToString(), out OverlayPanelLayout? saved) ? saved : null;

        private void SaveLayout()
        {
            try
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
                            panels[name] = previous;
                        }

                        continue;
                    }

                    panels[name] = new OverlayPanelLayout
                    {
                        Open = panel.IsVisible,
                        Left = panel.Position.X,
                        Top = panel.Position.Y,
                        Width = panel.PanelSize.Width,
                        Height = panel.PanelSize.Height,
                        Depth = panel.Depth,
                        SurfaceWidth = panel.SurfaceSize.Width,
                        SurfaceHeight = panel.SurfaceSize.Height
                    };
                }

                string serialised = JsonSerializer.Serialize(panels);

                if (serialised == _savedLayout)
                    return;

                _savedLayout = serialised;

                App.OverlayLayout.Prop.Panels = panels;
                App.OverlayLayout.Save();
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
            Show();
            Hide();
            Opacity = 1;

            InitialiseWin32();
        }

        public void Toggle()
        {
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

            _overlay?.DismissToast();

            if (_overlay?.IsGameForeground() == false)
                _overlay.FocusGame();

            if (!IsVisible)
                Show();

            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            Reanchor();

            Activate();

            Dispatcher.UIThread.Post(() => _presenting = false, DispatcherPriority.ApplicationIdle);
        }

        public void Reanchor()
        {
            if (OperatingSystem.IsWindows())
                _overlay?.AnchorAboveGame((IntPtr)_hwnd);
        }

        private void Window_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            foreach (OverlayPanel panel in PanelSurface.Children.OfType<OverlayPanel>())
                panel.Clamp();
        }

        private async void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            foreach (OverlayPanelKind kind in Enum.GetValues<OverlayPanelKind>())
            {
                if (PanelFor(kind).IsVisible)
                    Place(kind);
            }

            RestoreDepth();

            await _viewModel.OnLoaded();
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

            var modifiers = HOT_KEY_MODIFIERS.MOD_CONTROL |
                            HOT_KEY_MODIFIERS.MOD_ALT |
                            HOT_KEY_MODIFIERS.MOD_NOREPEAT;

            _hotkeyRegistered = PInvoke.RegisterHotKey(
                _hwnd, ToggleHotkeyId, modifiers, (uint)KeyInterop.VirtualKeyFromKey(Key.L));

            if (!_hotkeyRegistered)
                App.Logger.Warn("Could not register the overlay hotkey, something else owns Ctrl+Alt+L");

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

        protected override void OnClosed(EventArgs e)
        {
            NotesPad.Flush();

            SaveLayout();

            if (OperatingSystem.IsWindows() && _hotkeyRegistered)
                PInvoke.UnregisterHotKey(_hwnd, ToggleHotkeyId);

            base.OnClosed(e);
        }
    }
}