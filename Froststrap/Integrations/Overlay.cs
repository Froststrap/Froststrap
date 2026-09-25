using Avalonia;
using Avalonia.Threading;
using Froststrap.Integrations.OverlayModules;
using Froststrap.UI.Elements.Overlay;
using Froststrap;
using Froststrap.Integrations;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Froststrap.Integrations
{
    internal class Overlay : IDisposable
    {
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
        private const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
        private const uint EVENT_OBJECT_DESTROY = 0x8001;
        private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        public event EventHandler<OverlayBounds>? BoundsChanged;
        public event EventHandler<bool>? GameVisibilityChanged;
        public event EventHandler? WindowClosed;

        public readonly RealtimeMessaging Messaging = new();
        public readonly ActivityWatcher? ActivityWatcher;

        private readonly HWND _robloxWindow;
        private readonly uint _robloxProcessId;

        private WINEVENTPROC? _systemCallback;
        private WINEVENTPROC? _objectCallback;

        private UnhookWinEventSafeHandle? _systemHook;
        private UnhookWinEventSafeHandle? _objectHook;

        private GameOverlay? _window;
        private OverlayToast? _toast;
        private OverlayBounds? _lastBounds;
        private bool _disposed;

        public Overlay(long windowHandle, long robloxProcessId, ActivityWatcher? activityWatcher)
        {
            _robloxWindow = (HWND)(IntPtr)windowHandle;
            _robloxProcessId = (uint)robloxProcessId;
            ActivityWatcher = activityWatcher;
        }

        public void Start()
        {
            if (_robloxWindow == IntPtr.Zero)
            {
                App.Logger.Warn("No window handle, not starting");
                return;
            }

            App.Logger.Info($"Attaching to window {(IntPtr)_robloxWindow}");

            Dispatcher.UIThread.Invoke(() =>
            {
                _window = new GameOverlay(this);
                _window.PrepareHidden();

                _systemCallback = new WINEVENTPROC(OnSystemEvent);
                _objectCallback = new WINEVENTPROC(OnObjectEvent);

                _systemHook = PInvoke.SetWinEventHook(
                    EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_MINIMIZEEND,
                    null, _systemCallback, _robloxProcessId, 0, WINEVENT_OUTOFCONTEXT);

                _objectHook = PInvoke.SetWinEventHook(
                    EVENT_OBJECT_DESTROY, EVENT_OBJECT_LOCATIONCHANGE,
                    null, _objectCallback, _robloxProcessId, 0, WINEVENT_OUTOFCONTEXT);
            });

            SyncBounds();
            Messaging.ConnectToUserhub();
        }

        public void SyncBounds()
        {
            OverlayBounds bounds = GetBounds();
            _lastBounds = bounds;

            Dispatcher.UIThread.Post(() => BoundsChanged?.Invoke(this, bounds));
        }

        public void AnchorAboveGame(IntPtr overlayHandle)
        {
            if (overlayHandle == IntPtr.Zero || _robloxWindow == IntPtr.Zero)
                return;

            PInvoke.SetWindowPos(
                (HWND)overlayHandle,
                HWND.Null,
                0, 0, 0, 0,
                SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
                SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        }

        public bool ShowToast(string title, string message)
        {
            if (_disposed || _window is null || _robloxWindow == IntPtr.Zero)
            {
                App.Logger.Warn("No overlay to show it in");
                return false;
            }

            if (IsGameMinimised() || !IsGameForeground())
            {
                App.Logger.Info("Game isn't in front, leaving it to the desktop notification");
                return false;
            }

            return Dispatcher.UIThread.Invoke(() =>
            {
                try
                {
                    OverlayBounds bounds = GetBounds();

                    if (bounds.Rect.Width <= 0 || bounds.Rect.Height <= 0)
                    {
                        App.Logger.Warn("Could not measure the game window");
                        return false;
                    }

                    App.Logger.Info($"{title}: {message.Replace("\n", "\\n")}");

                    _toast ??= new OverlayToast();
                    _toast.Present(title, message, bounds.Rect);

                    return true;
                }
                catch (Exception ex)
                {
                    App.Logger.Error("Failed to show a toast");
                    App.Logger.Error(ex);
                    return false;
                }
            });
        }

        public void DismissToast() => _toast?.Dismiss();

        public bool IsGameMinimised() => PInvoke.IsIconic(_robloxWindow);

        public bool IsGameForeground() => PInvoke.GetForegroundWindow() == _robloxWindow;

        public void FocusGame() => PInvoke.SetForegroundWindow(_robloxWindow);

        private OverlayBounds GetBounds()
        {
            if (!PInvoke.GetClientRect(_robloxWindow, out RECT client))
                return new OverlayBounds(default, false);

            POINT origin = new() { X = client.left, Y = client.top };

            if (!PInvoke.ClientToScreen(_robloxWindow, ref origin))
                return new OverlayBounds(default, false);

            var bounds = new Rect(
                origin.X,
                origin.Y,
                Math.Max(client.right - client.left, 0),
                Math.Max(client.bottom - client.top, 0));

            return new OverlayBounds(bounds, PInvoke.IsZoomed(_robloxWindow));
        }

        private void OnSystemEvent(HWINEVENTHOOK hook, uint iEvent, HWND hWnd, int idObject, int idChild, uint thread, uint time)
        {
            if (hWnd != _robloxWindow)
                return;

            switch (iEvent)
            {
                case EVENT_SYSTEM_MINIMIZESTART:
                    Dispatcher.UIThread.Post(() => GameVisibilityChanged?.Invoke(this, false));
                    break;

                case EVENT_SYSTEM_MINIMIZEEND:
                    Dispatcher.UIThread.Post(() => GameVisibilityChanged?.Invoke(this, true));
                    SyncBounds();
                    break;

                case EVENT_SYSTEM_FOREGROUND:
                    Dispatcher.UIThread.Post(() => _window?.Reanchor());
                    break;
            }
        }

        private void OnObjectEvent(HWINEVENTHOOK hook, uint iEvent, HWND hWnd, int idObject, int idChild, uint thread, uint time)
        {
            if (hWnd != _robloxWindow)
                return;

            if (iEvent == EVENT_OBJECT_DESTROY)
            {
                App.Logger.Info("Game window went away");
                Dispatcher.UIThread.Post(() => WindowClosed?.Invoke(this, EventArgs.Empty));
                return;
            }

            try
            {
                PublishBounds();
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to track the game window");
                App.Logger.Error(ex);
            }
        }

        private void PublishBounds()
        {
            OverlayBounds bounds = GetBounds();

            if (bounds == _lastBounds)
                return;

            _lastBounds = bounds;
            BoundsChanged?.Invoke(this, bounds);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _systemHook?.Dispose();
            _objectHook?.Dispose();
            _systemHook = null;
            _objectHook = null;
            _systemCallback = null;
            _objectCallback = null;

            Dispatcher.UIThread.Post(() =>
            {
                _toast?.Close();
                _window?.Close();
            });

            _ = Messaging.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}