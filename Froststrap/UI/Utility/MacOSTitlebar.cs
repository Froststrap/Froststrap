// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

// TODO:
// Rip out all off IsThick

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Froststrap.UI.Utility;

internal static class MacOSTitleBar
{
    // ── attached properties ────────────────────────────────

    /// <summary>
    /// Adds an empty NSToolbar so macOS uses the taller unified title bar.
    /// </summary>
    public static readonly AttachedProperty<bool> IsThickProperty =
        AvaloniaProperty.RegisterAttached<Window, bool>(
            "IsThick", typeof(MacOSTitleBar), defaultValue: false);

    public static bool GetIsThick(Window window) => window.GetValue(IsThickProperty);
    public static void SetIsThick(Window window, bool value) => window.SetValue(IsThickProperty, value);

    /// <summary>
    /// Position of the traffic-light group's top-left corner (the close button's
    /// top-left), in points, measured from the window's top-left. null = system default.
    /// Works with or without IsThick.
    /// </summary>
    public static readonly AttachedProperty<Point?> TrafficLightOffsetProperty =
        AvaloniaProperty.RegisterAttached<Window, Point?>(
            "TrafficLightOffset", typeof(MacOSTitleBar), defaultValue: null);

    public static Point? GetTrafficLightOffset(Window window) => window.GetValue(TrafficLightOffsetProperty);
    public static void SetTrafficLightOffset(Window window, Point? value) => window.SetValue(TrafficLightOffsetProperty, value);

    static MacOSTitleBar()
    {
        IsThickProperty.Changed.AddClassHandler<Window>((w, _) => Sync(w));
        TrafficLightOffsetProperty.Changed.AddClassHandler<Window>((w, _) => Sync(w));
    }

    // ── per-window state ───────────────────────────────────
    // ConditionalWeakTable ensures state is garbage-collected alongside the
    // Window instance, preventing leaks when windows are closed.

    private static readonly ConditionalWeakTable<Window, TitleBarState> States = new();

    /// <summary>
    /// Creates, updates or tears down the per-window state depending on which
    /// properties are currently set.
    /// </summary>
    private static void Sync(Window window)
    {
        // no-op on non-macOS — safe to leave the properties in cross-platform XAML/code
        if (!OperatingSystem.IsMacOS())
            return;

        bool wanted = GetIsThick(window) || GetTrafficLightOffset(window) is not null;

        if (!wanted)
        {
            if (States.TryGetValue(window, out var old))
            {
                old.Detach();
                States.Remove(window);
            }

            return;
        }

        if (States.TryGetValue(window, out var state))
        {
            state.Update();
        }
        else
        {
            state = new TitleBarState(window);
            States.Add(window, state);
            state.Attach();
        }
    }

    // ── per-window lifecycle ───────────────────────────────

    private sealed class TitleBarState
    {
        // NSWindowButton values
        private const long CloseButton = 0;
        private const long MiniaturizeButton = 1;
        private const long ZoomButton = 2;

        private readonly Window _window;
        private IntPtr _nsWindow;
        private IntPtr _nsToolbar; // zero when the thick toolbar isn't active
        private IntPtr _selSetToolbar;
        private IntPtr _selSetTitlebarAppearsTransparent;
        private IntPtr _selSetTitleVisibility;
        private IntPtr _selStandardWindowButton;
        private IntPtr _selSuperview;
        private IntPtr _selFrame;
        private IntPtr _selSetFrame;
        private IntPtr _selIsFlipped;
        private bool _attached;

        // default (system) frames of close/minimize/zoom, captured the first time we
        // move them. used for relative spacing and to restore the default layout.
        private readonly CGRect[] _originalFrames = new CGRect[3];
        private bool _haveOriginals;

        public TitleBarState(Window window) => _window = window;

        public void Attach()
        {
            if (_attached)
                return;

            // the NSWindow handle is only available after the native window is
            // created — defer setup if the window hasn't opened yet
            if (_window.IsLoaded)
                Setup();
            else
                _window.Opened += OnOpened;
        }

        public void Detach()
        {
            // always unhook, even if Setup never ran (window never opened)
            _window.Opened -= OnOpened;
            _window.PropertyChanged -= OnWindowPropertyChanged;
            _window.Resized -= OnWindowResized;

            if (!_attached)
                return;

            // put the traffic lights back first, then drop the toolbar
            try { RestoreTrafficLights(); } catch { /* best effort */ }
            RemoveToolbar();

            _attached = false;
        }

        /// <summary>
        /// Re-reads IsThick / TrafficLightOffset and applies them. Safe to call any time.
        /// </summary>
        public void Update()
        {
            if (!_attached)
                return;

            try
            {
                bool wantToolbar = GetIsThick(_window);

                if (wantToolbar && _nsToolbar == IntPtr.Zero)
                    CreateToolbar();
                else if (!wantToolbar && _nsToolbar != IntPtr.Zero)
                    RemoveToolbar();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("macOS title bar toolbar update failed: {0}", ex.Message);
            }

            PostApplyTrafficLights();
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            _window.Opened -= OnOpened;
            Setup();
        }

        private void Setup()
        {
            try
            {
                // 1. get the native NSWindow handle from Avalonia's platform interop.
                //    this is the actual Cocoa window object that AppKit manages.
                _nsWindow = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (_nsWindow == IntPtr.Zero)
                    return;

                // 2. cache ObjC selectors. sel_registerName returns a stable
                //    pointer for the lifetime of the process.
                _selSetToolbar = sel_registerName("setToolbar:");
                _selSetTitlebarAppearsTransparent = sel_registerName("setTitlebarAppearsTransparent:");
                _selSetTitleVisibility = sel_registerName("setTitleVisibility:");
                _selStandardWindowButton = sel_registerName("standardWindowButton:");
                _selSuperview = sel_registerName("superview");
                _selFrame = sel_registerName("frame");
                _selSetFrame = sel_registerName("setFrame:");
                _selIsFlipped = sel_registerName("isFlipped");

                // 3. subscribe to WindowState changes to handle fullscreen transitions
                //    (toolbar removal, button reparenting).
                _window.PropertyChanged += OnWindowPropertyChanged;

                // 4. AppKit re-lays out the traffic lights on resize, which resets any
                //    frames we set — so re-apply our placement after every resize.
                _window.Resized += OnWindowResized;

                _attached = true;

                // 5. create the toolbar (if IsThick) and position the traffic lights
                Update();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("macOS title bar setup failed: {0}", ex.Message);
            }
        }

        // ── thick toolbar ──────────────────────────────────

        private void CreateToolbar()
        {
            // create an empty NSToolbar. assigning ANY toolbar to an NSWindow causes
            // macOS to switch to the "unified toolbar" layout with a taller title bar.
            // the toolbar itself has no items — it's purely a trigger for the height.
            var toolbar = objc_msgSend_ret(
                objc_getClass("NSToolbar"), sel_registerName("alloc"));

            toolbar = objc_msgSend_arg_ret(
                toolbar,
                sel_registerName("initWithIdentifier:"),
                CreateNSString("filamentTitleBar"));

            if (toolbar == IntPtr.Zero)
                return;

            // hide the baseline separator line between the toolbar and content.
            // deprecated since macOS 11 (Big Sur), but a harmless no-op on modern versions.
            objc_msgSend_bool(toolbar, sel_registerName("setShowsBaselineSeparator:"), false);

            _nsToolbar = toolbar;

            // assign the toolbar to the window — this is what triggers the height increase
            objc_msgSend_arg(_nsWindow, _selSetToolbar, _nsToolbar);

            // hide the window title text (NSWindowTitleHidden = 1).
            // we render our own title bar content in Avalonia.
            objc_msgSend_long(_nsWindow, _selSetTitleVisibility, 1);
        }

        private void RemoveToolbar()
        {
            if (_nsToolbar == IntPtr.Zero)
                return;

            // remove the toolbar to restore the default thin title bar
            objc_msgSend_arg(_nsWindow, _selSetToolbar, IntPtr.Zero);

            // we own the +1 retain from alloc/init; the window released its own
            objc_msgSend_ret(_nsToolbar, sel_registerName("release"));
            _nsToolbar = IntPtr.Zero;
        }

        // ── traffic lights ─────────────────────────────────

        private void PostApplyTrafficLights() =>
            Dispatcher.UIThread.Post(ApplyTrafficLights, DispatcherPriority.Background);

        /// <summary>
        /// Moves the close/minimize/zoom buttons so the group's top-left sits at the
        /// TrafficLightOffset (from the window's top-left). null restores the default.
        /// </summary>
        public void ApplyTrafficLights()
        {
            if (!_attached || _nsWindow == IntPtr.Zero)
                return;

            // in fullscreen the buttons live in a different (menu-bar) container
            if (_window.WindowState == Avalonia.Controls.WindowState.FullScreen)
                return;

            try
            {
                if (!TryGetButtons(out var buttons))
                    return;

                // capture the system layout once, before we ever touch it
                if (!_haveOriginals)
                {
                    for (int i = 0; i < 3; i++)
                        _originalFrames[i] = GetFrame(buttons[i]);
                    _haveOriginals = true;
                }

                if (GetTrafficLightOffset(_window) is not { } offset)
                {
                    RestoreTrafficLights(buttons);
                    return;
                }

                // the buttons' parent (NSTitlebarView) defines the coordinate space
                var host = objc_msgSend_ret(buttons[0], _selSuperview);
                if (host == IntPtr.Zero)
                    return;

                double hostHeight = GetFrame(host).Height;
                bool flipped = objc_msgSend_bool_ret(host, _selIsFlipped);

                // keep the buttons inside the title bar region so they can't be clipped away
                var anchor = _originalFrames[0];
                double maxTop = Math.Max(0, hostHeight - anchor.Height);
                double top = Math.Clamp(offset.Y, 0, maxTop);
                double dx = Math.Max(0, offset.X) - anchor.X;

                for (int i = 0; i < 3; i++)
                {
                    var f = _originalFrames[i];
                    f.X += dx; // shifts the whole group, preserving the system spacing
                    f.Y = flipped ? top : hostHeight - top - f.Height;
                    SetFrame(buttons[i], f);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("macOS traffic light placement failed: {0}", ex.Message);
            }
        }

        private void RestoreTrafficLights()
        {
            if (_haveOriginals && TryGetButtons(out var buttons))
                RestoreTrafficLights(buttons);
        }

        private void RestoreTrafficLights(IntPtr[] buttons)
        {
            if (!_haveOriginals)
                return;

            for (int i = 0; i < 3; i++)
                SetFrame(buttons[i], _originalFrames[i]);
        }

        private bool TryGetButtons(out IntPtr[] buttons)
        {
            buttons =
            [
                objc_msgSend_long_ret(_nsWindow, _selStandardWindowButton, CloseButton),
                objc_msgSend_long_ret(_nsWindow, _selStandardWindowButton, MiniaturizeButton),
                objc_msgSend_long_ret(_nsWindow, _selStandardWindowButton, ZoomButton),
            ];

            return buttons[0] != IntPtr.Zero && buttons[1] != IntPtr.Zero && buttons[2] != IntPtr.Zero;
        }

        private CGRect GetFrame(IntPtr view)
        {
            // CGRect is 32 bytes: arm64 returns it in registers via plain objc_msgSend,
            // x86_64 returns it through a hidden pointer via objc_msgSend_stret.
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                return objc_msgSend_rect(view, _selFrame);

            objc_msgSend_stret(out var rect, view, _selFrame);
            return rect;
        }

        private void SetFrame(IntPtr view, CGRect frame) =>
            objc_msgSend_setrect(view, _selSetFrame, frame);

        private void OnWindowResized(object? sender, WindowResizedEventArgs e)
        {
            // apply immediately: during a live resize the run loop is in event-tracking mode
            // and Background-priority posts don't run until the drag ends
            ApplyTrafficLights();

            // safety net in case AppKit lays out again after this callback
            PostApplyTrafficLights();
        }

        // ── window state ───────────────────────────────────

        private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != Window.WindowStateProperty)
                return;

            var newState = e.GetNewValue<Avalonia.Controls.WindowState>();

            // use Background priority so this runs AFTER Avalonia's own fullscreen
            // handlers, which set titlebarAppearsTransparent = NO during transitions.
            Dispatcher.UIThread.Post(() =>
            {
                // we may have been detached between the post and now
                if (!_attached)
                    return;

                try
                {
                    if (newState == Avalonia.Controls.WindowState.FullScreen)
                    {
                        // the buttons get reparented in fullscreen, so the frames we
                        // captured are stale — recapture after we come back
                        _haveOriginals = false;

                        if (_nsToolbar != IntPtr.Zero)
                        {
                            // detach (not release) the toolbar — left attached, macOS
                            // renders it as an opaque gray bar in the fullscreen space
                            objc_msgSend_arg(_nsWindow, _selSetToolbar, IntPtr.Zero);

                            // Avalonia sets titlebarAppearsTransparent = NO on fullscreen
                            // entry; override it back so the region blends with our content
                            objc_msgSend_bool(_nsWindow, _selSetTitlebarAppearsTransparent, true);
                            objc_msgSend_long(_nsWindow, _selSetTitleVisibility, 1);
                        }
                    }
                    else
                    {
                        if (_nsToolbar != IntPtr.Zero)
                        {
                            // re-attach the toolbar to restore the thick title bar
                            objc_msgSend_arg(_nsWindow, _selSetToolbar, _nsToolbar);
                            objc_msgSend_bool(_nsWindow, _selSetTitlebarAppearsTransparent, true);
                            objc_msgSend_long(_nsWindow, _selSetTitleVisibility, 1);
                        }

                        // buttons were reset by AppKit during the transition
                        ApplyTrafficLights();
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("macOS title bar fullscreen toggle failed: {0}", ex.Message);
                }
            }, DispatcherPriority.Background);
        }
    }

    // ── ObjC helpers ───────────────────────────────────────

    private static IntPtr CreateNSString(string str)
    {
        var nsString = objc_msgSend_ret(objc_getClass("NSString"), sel_registerName("alloc"));
        var utf8Ptr = Marshal.StringToCoTaskMemUTF8(str);
        try
        {
            return objc_msgSend_arg_ret(nsString, sel_registerName("initWithUTF8String:"), utf8Ptr);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8Ptr);
        }
    }

    // NSRect/CGRect: origin (x, y) followed by size (width, height), all doubles
    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
    }

    // ── P/Invoke: ObjC runtime ─────────────────────────────

    [DllImport("/usr/lib/libobjc.A.dylib", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ret(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_arg(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_arg_ret(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_long(IntPtr receiver, IntPtr selector, long arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_long_ret(IntPtr receiver, IntPtr selector, long arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_bool_ret(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern CGRect objc_msgSend_rect(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend_stret")]
    private static extern void objc_msgSend_stret(out CGRect result, IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_setrect(IntPtr receiver, IntPtr selector, CGRect rect);
}
