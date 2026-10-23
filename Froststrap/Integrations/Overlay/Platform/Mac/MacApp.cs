// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;

namespace Froststrap.Integrations.Overlay.Platform.Mac
{
    internal static class MacApp
    {
        private static IntPtr? _nsWorkspaceClass;
        private static IntPtr? _runningApplicationClass;

        public static int FrontmostPid()
        {
            IntPtr workspaceClass = _nsWorkspaceClass ??= MacObjC.Class("NSWorkspace");
            IntPtr workspace = MacObjC.SendPtr(workspaceClass, MacObjC.Sel("sharedWorkspace"));

            if (workspace == IntPtr.Zero)
                return 0;

            IntPtr application = MacObjC.SendPtr(workspace, MacObjC.Sel("frontmostApplication"));

            return application == IntPtr.Zero
                ? 0
                : MacObjC.SendInt(application, MacObjC.Sel("processIdentifier"));
        }

        public static bool IsOwnProcessForeground() => FrontmostPid() == Environment.ProcessId;

        public static bool IsHidden(int processId)
        {
            IntPtr application = RunningApplication(processId);

            return application != IntPtr.Zero
                && MacObjC.SendByte(application, MacObjC.Sel("isHidden")) != 0;
        }

        public static void Activate(int processId)
        {
            IntPtr application = RunningApplication(processId);

            if (application == IntPtr.Zero)
                return;

            MacObjC.SendByteUIntPtr(application, MacObjC.Sel("activateWithOptions:"), 3);
        }

        private static IntPtr RunningApplication(int processId)
        {
            IntPtr clazz = _runningApplicationClass ??= MacObjC.Class("NSRunningApplication");

            return MacObjC.SendPtrInt(
                clazz,
                MacObjC.Sel("runningApplicationWithProcessIdentifier:"),
                processId);
        }
    }

    internal sealed class MacWindow
    {
        internal enum WindowKind
        {
            Game,
            KeyOverlay,
            SearchBar,
            PinnedPanel
        }

        private const long OverlayWindowLevel = 25;

        private const nuint OverlayCollectionBehavior =
            (1 << 0)
            | (1 << 4)
            | (1 << 6)
            | (1 << 8);

        private const long AnimationBehaviorNone = 2;

        private readonly Window _window;
        private readonly WindowKind _kind;

        private IntPtr _nsWindow;
        private bool _clickThrough;
        private bool _cloaked;

        public MacWindow(Window window, WindowKind kind)
        {
            _window = window;
            _kind = kind;
        }

        public bool Attach()
        {
            var handle = _window.TryGetPlatformHandle();

            if (handle is null || handle.Handle == IntPtr.Zero)
                return false;

            _nsWindow = handle.Handle;

            ApplyBaseWindowBehaviour();

            SetClickThrough(_kind == WindowKind.KeyOverlay);

            return true;
        }

        private void ApplyBaseWindowBehaviour()
        {
            MacObjC.SendVoidLong(_nsWindow, MacObjC.Sel("setLevel:"), OverlayWindowLevel);
            MacObjC.SendVoidUIntPtr(_nsWindow, MacObjC.Sel("setCollectionBehavior:"), OverlayCollectionBehavior);
            MacObjC.SendVoidByte(_nsWindow, MacObjC.Sel("setHidesOnDeactivate:"), 0);
            MacObjC.SendVoidByte(_nsWindow, MacObjC.Sel("setCanHide:"), 0);
            MacObjC.SendVoidByte(_nsWindow, MacObjC.Sel("setHasShadow:"), 0);
            MacObjC.SendVoidByte(_nsWindow, MacObjC.Sel("setOpaque:"), 0);
            MacObjC.SendVoidLong(_nsWindow, MacObjC.Sel("setAnimationBehavior:"), AnimationBehaviorNone);
            MacObjC.SendVoidByte(_nsWindow, MacObjC.Sel("setMovable:"), 0);
            MacObjC.SendVoidByte(_nsWindow, MacObjC.Sel("setMovableByWindowBackground:"), 0);

            IntPtr nsColorClass = MacObjC.Class("NSColor");
            IntPtr clear = MacObjC.SendPtr(nsColorClass, MacObjC.Sel("clearColor"));

            if (clear != IntPtr.Zero)
                MacObjC.SendVoidPtr(_nsWindow, MacObjC.Sel("setBackgroundColor:"), clear);
        }

        public void SetClickThrough(bool clickThrough)
        {
            if (_nsWindow == IntPtr.Zero || _clickThrough == clickThrough)
                return;

            _clickThrough = clickThrough;
            MacObjC.SendVoidByte(
                _nsWindow,
                MacObjC.Sel("setIgnoresMouseEvents:"),
                clickThrough ? (byte)1 : (byte)0);
        }

        public void SetCloaked(bool cloaked)
        {
            if (_nsWindow == IntPtr.Zero || _cloaked == cloaked)
                return;

            _cloaked = cloaked;

            if (cloaked)
            {
                MacObjC.SendVoidDouble(_nsWindow, MacObjC.Sel("setAlphaValue:"), 0);
                MacObjC.SendVoidPtr(_nsWindow, MacObjC.Sel("orderOut:"), IntPtr.Zero);
                return;
            }

            MacObjC.SendVoidDouble(_nsWindow, MacObjC.Sel("setAlphaValue:"), 1);

            if (_window.IsVisible)
                MacObjC.SendVoid(_nsWindow, MacObjC.Sel("orderFrontRegardless"));
        }

        public bool IsForeground() =>
            !_cloaked && _window.IsVisible && MacApp.IsOwnProcessForeground();

        public void Raise()
        {
            if (_nsWindow != IntPtr.Zero && !_cloaked && _window.IsVisible)
                MacObjC.SendVoid(_nsWindow, MacObjC.Sel("orderFrontRegardless"));
        }

        public void Release()
        {
            _nsWindow = IntPtr.Zero;
            _clickThrough = false;
            _cloaked = false;
        }
    }
}