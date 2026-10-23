// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

using Froststrap.Models.Overlay;

namespace Froststrap.Integrations.Overlay.Platform.Mac
{
    internal static class MacDisplay
    {
        public static OverlayBounds ToBounds(CGRect rect, Screens? screens)
        {
            uint display = DisplayFor(rect);
            CGRect displayBounds = MacCG.CGDisplayBounds(display);

            Screen? screen = null;

            if (screens is not null)
            {
                foreach (Screen candidate in screens.All)
                {
                    IPlatformHandle? handle = candidate.TryGetPlatformHandle();

                    if (handle is not null && handle.Handle == (IntPtr)display)
                    {
                        screen = candidate;
                        break;
                    }
                }
            }

            if (screen is null && screens?.Primary is Screen primary)
                screen = primary;

            if (screen is null || displayBounds.Width <= 0 || displayBounds.Height <= 0)
                return new OverlayBounds(new Rect(rect.X, rect.Y, rect.Width, rect.Height), false);

            double scaleX = screen.Bounds.Width / displayBounds.Width;
            double scaleY = screen.Bounds.Height / displayBounds.Height;

            double left = screen.Bounds.X + ((rect.X - displayBounds.X) * scaleX);
            double top = screen.Bounds.Y + ((rect.Y - displayBounds.Y) * scaleY);

            bool maximised =
                rect.Width >= displayBounds.Width - 1
                && rect.Height >= displayBounds.Height - 1;

            return new OverlayBounds(
                new Rect(left, top, Math.Max(rect.Width * scaleX, 0), Math.Max(rect.Height * scaleY, 0)),
                maximised);
        }

        private static uint DisplayFor(CGRect rect)
        {
            var displays = new uint[8];

            if (MacCG.CGGetDisplaysWithRect(rect, (uint)displays.Length, displays, out uint count) == 0
                && count > 0)
            {
                return displays[0];
            }

            return MacCG.CGMainDisplayID();
        }
    }

    internal sealed class MacWindowTracker : IDisposable
    {
        private const int MissingTicksBeforeClosed = 5;

        private static readonly IntPtr OwnerPidKey = MacCF.String("kCGWindowOwnerPID");
        private static readonly IntPtr LayerKey = MacCF.String("kCGWindowLayer");
        private static readonly IntPtr BoundsKey = MacCF.String("kCGWindowBounds");
        private static readonly IntPtr OnScreenKey = MacCF.String("kCGWindowIsOnscreen");

        private readonly int _processId;
        private readonly DispatcherTimer _timer;

        private OverlayBounds? _bounds;
        private bool _visible;
        private bool _minimised;
        private int _missingTicks;
        private int _lastForeground = -1;
        private bool _closed;
        private bool _disposed;

        public MacWindowTracker(int processId)
        {
            _processId = processId;
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, OnTick);
        }

        public Screens? Screens { get; set; }

        public OverlayBounds? Bounds => _bounds;

        public bool IsVisible => _visible;

        public bool IsMinimised => _minimised;

        public event EventHandler<OverlayBounds>? BoundsChanged;
        public event EventHandler<bool>? VisibilityChanged;
        public event EventHandler? Closed;

        public void Start()
        {
            if (_disposed)
                return;

            _timer.Start();
            Refresh();
        }

        public bool TryGetWindow(out CGRect rect, out bool onScreen)
        {
            WindowInfo? info = FindWindow();

            if (info is null)
            {
                rect = default;
                onScreen = false;
                return false;
            }

            rect = info.Value.Bounds;
            onScreen = info.Value.OnScreen;
            return true;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            try
            {
                Refresh();
            }
            catch (Exception ex)
            {
                App.Logger.Error("MacWindowTracker: refresh failed");
                App.Logger.Error(ex);
            }
        }

        public void Refresh()
        {
            if (_disposed)
                return;

            WindowInfo? info = FindWindow();

            if (info is null)
            {
                _missingTicks++;

                if (_missingTicks == MissingTicksBeforeClosed && !_closed)
                {
                    _closed = true;
                    Closed?.Invoke(this, EventArgs.Empty);
                }

                UpdateVisibility(false, true);
            }
            else
            {
                _missingTicks = 0;

                if (Screens is not null)
                {
                    OverlayBounds bounds = MacDisplay.ToBounds(info.Value.Bounds, Screens);

                    if (bounds != _bounds)
                    {
                        _bounds = bounds;
                        BoundsChanged?.Invoke(this, bounds);
                    }
                }

                bool hidden = MacApp.IsHidden(_processId);
                bool onScreen = info.Value.OnScreen && !hidden;

                UpdateVisibility(onScreen, hidden || !onScreen);
            }

            int foreground = MacApp.FrontmostPid();

            if (foreground != _lastForeground)
            {
                _lastForeground = foreground;
                ForegroundChanged?.Invoke(this, foreground);
            }
        }

        public event EventHandler<int>? ForegroundChanged;

        private void UpdateVisibility(bool visible, bool minimised)
        {
            if (_visible != visible)
            {
                _visible = visible;
                VisibilityChanged?.Invoke(this, visible);
            }

            _minimised = minimised;
        }

        private WindowInfo? FindWindow()
        {
            IntPtr array = MacCG.CGWindowListCopyWindowInfo(
                MacCG.WindowListOptionAll | MacCG.WindowListExcludeDesktopElements,
                0);

            if (array == IntPtr.Zero)
                return null;

            try
            {
                nint count = MacCF.CFArrayGetCount(array);
                WindowInfo? best = null;

                for (nint i = 0; i < count; i++)
                {
                    IntPtr dictionary = MacCF.CFArrayGetValueAtIndex(array, i);

                    if (dictionary == IntPtr.Zero)
                        continue;

                    if (GetInt(dictionary, OwnerPidKey) != _processId)
                        continue;

                    if (GetInt(dictionary, LayerKey) != 0)
                        continue;

                    IntPtr boundsValue = MacCF.CFDictionaryGetValue(dictionary, BoundsKey);

                    if (boundsValue == IntPtr.Zero
                        || MacCG.CGRectMakeWithDictionaryRepresentation(boundsValue, out CGRect rect) == 0
                        || rect.IsEmpty)
                    {
                        continue;
                    }

                    bool onScreen = GetBool(dictionary, OnScreenKey);
                    var current = new WindowInfo(rect, onScreen);

                    if (best is null)
                    {
                        best = current;
                        continue;
                    }

                    if (current.OnScreen != best.Value.OnScreen)
                    {
                        if (current.OnScreen)
                            best = current;

                        continue;
                    }

                    if (current.Bounds.Area > best.Value.Bounds.Area)
                        best = current;
                }

                return best;
            }
            finally
            {
                MacCF.Release(array);
            }
        }

        private static int GetInt(IntPtr dictionary, IntPtr key)
        {
            IntPtr number = MacCF.CFDictionaryGetValue(dictionary, key);

            return number != IntPtr.Zero
                && MacCF.CFNumberGetValue(number, MacCF.NumberSInt32Type, out int value) != 0
                ? value
                : 0;
        }

        private static bool GetBool(IntPtr dictionary, IntPtr key)
        {
            IntPtr boolean = MacCF.CFDictionaryGetValue(dictionary, key);

            return boolean != IntPtr.Zero && MacCF.CFBooleanGetValue(boolean) != 0;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (Dispatcher.UIThread.CheckAccess())
                _timer.Stop();
            else
                Dispatcher.UIThread.Post(_timer.Stop);

            GC.SuppressFinalize(this);
        }

        private readonly record struct WindowInfo(CGRect Bounds, bool OnScreen);
    }
}
