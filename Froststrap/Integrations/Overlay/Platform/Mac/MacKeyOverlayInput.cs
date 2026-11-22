// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Froststrap.Enums;
using Froststrap.Models.Overlay;

namespace Froststrap.Integrations.Overlay.Platform.Mac
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2216",
        Justification = "The event tap thread and GC handle keep this instance rooted, so a finalizer could never run before Dispose; the OS removes the tap on process exit.")]
    internal sealed class MacKeyOverlayInput : IKeyOverlayInput
    {
        private const uint SpaceKeyCode = 0x31;

        private const ulong EventMask =
            (1UL << (int)MacCG.EventKeyDown)
            | (1UL << (int)MacCG.EventKeyUp)
            | (1UL << (int)MacCG.EventFlagsChanged)
            | (1UL << (int)MacCG.EventLeftMouseDown)
            | (1UL << (int)MacCG.EventLeftMouseUp)
            | (1UL << (int)MacCG.EventRightMouseDown)
            | (1UL << (int)MacCG.EventRightMouseUp);

        private readonly Func<bool> _isGameForeground;
        private readonly KeyOverlayState _state;

        private readonly GCHandle _selfHandle;

        private Thread? _thread;
        private IntPtr _tap;
        private IntPtr _source;
        private IntPtr _runLoop;

        private volatile bool _isDisposed;
        private int _resourcesFreed;

        public MacKeyOverlayInput(SoftKeyProfile profile, Func<bool> isGameForeground)
        {
            _isGameForeground = isGameForeground;

            var keys = MacSoftKeyLayout.Resolve(profile);

            _state = new KeyOverlayState(keys.Up, keys.Left, keys.Down, keys.Right, SpaceKeyCode);

            _selfHandle = GCHandle.Alloc(this, GCHandleType.Normal);

            _thread = new Thread(TapThread)
            {
                IsBackground = true,
                Name = "Key Overlay Event Tap"
            };

            _thread.Start();
        }

        public KeyOverlaySnapshot GetSnapshot() => _state.GetSnapshot();

        private void TapThread()
        {
            IntPtr runLoop = MacCF.CFRunLoopGetCurrent();

            _runLoop = runLoop;

            if (_isDisposed)
            {
                ReleaseTap();
                FreeHandle();
                return;
            }

            try
            {
                if (MacPermissions.IOHIDCheckAccess(MacPermissions.HidRequestTypeListenEvent) != 0)
                    MacPermissions.IOHIDRequestAccess(MacPermissions.HidRequestTypeListenEvent);

                if (MacPermissions.AXIsProcessTrusted() == 0)
                    App.Logger.Warn("Key overlay requires Accessibility permission for Froststrap");

                unsafe
                {
                    IntPtr callback =
                        (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, uint, IntPtr, IntPtr, IntPtr>)&OnEvent;

                    _tap = MacCG.CGEventTapCreate(
                        MacCG.EventTapLocationSession,
                        MacCG.EventTapPlacementHead,
                        MacCG.EventTapOptionListenOnly,
                        EventMask,
                        callback,
                        GCHandle.ToIntPtr(_selfHandle));
                }

                if (_tap == IntPtr.Zero)
                {
                    App.Logger.Warn(
                        "Key overlay could not create the macOS event tap. " +
                        "Grant Accessibility and Input Monitoring permission to Froststrap.");
                    return;
                }

                _source = MacCF.CFMachPortCreateRunLoopSource(IntPtr.Zero, _tap, 0);

                if (_source == IntPtr.Zero)
                {
                    App.Logger.Warn("Key overlay could not create the event tap run loop source");
                    return;
                }

                if (_isDisposed)
                    return;

                IntPtr mode = MacCF.String("kCFRunLoopCommonModes");

                try
                {
                    MacCF.CFRunLoopAddSource(runLoop, _source, mode);
                    MacCG.CGEventTapEnable(_tap, 1);

                    while (!_isDisposed)
                    {
                        int runLoopResult = MacCF.CFRunLoopRunInMode(mode, 0.25, 0);

                        if (runLoopResult == MacCF.RunLoopRunFinished)
                            break;
                    }
                }
                finally
                {
                    MacCF.Release(mode);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Key overlay event tap failed");
                App.Logger.Error(ex);
            }
            finally
            {
                ReleaseTap();
                FreeHandle();
            }
        }

        private void ReleaseTap()
        {
            IntPtr tap = Interlocked.Exchange(ref _tap, IntPtr.Zero);
            IntPtr source = Interlocked.Exchange(ref _source, IntPtr.Zero);

            MacCF.Release(source);
            MacCF.Release(tap);
        }

        private void FreeHandle()
        {
            if (Interlocked.Exchange(ref _resourcesFreed, 1) == 0 && _selfHandle.IsAllocated)
                _selfHandle.Free();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static IntPtr OnEvent(IntPtr proxy, uint type, IntPtr eventRef, IntPtr userInfo)
        {
            try
            {
                if (type == MacCG.EventTapDisabledByTimeout || type == MacCG.EventTapDisabledByUserInput)
                {
                    MacCG.CGEventTapEnable(proxy, 1);
                    return eventRef;
                }

                if (userInfo == IntPtr.Zero)
                    return eventRef;

                if (GCHandle.FromIntPtr(userInfo).Target is MacKeyOverlayInput input)
                    input.HandleEvent(type, eventRef);

                return eventRef;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Key overlay event tap crashed");
                return eventRef;
            }
        }

        private void HandleEvent(uint type, IntPtr eventRef)
        {
            if (_isDisposed)
                return;

            if (!_isGameForeground())
            {
                _state.Clear();
                return;
            }

            switch (type)
            {
                case MacCG.EventKeyDown:
                case MacCG.EventKeyUp:
                {
                    uint keyCode = (uint)MacCG.CGEventGetIntegerValueField(
                        eventRef,
                        MacCG.KeyboardEventKeycode);

                    _state.SetKey(keyCode, type == MacCG.EventKeyDown);
                    break;
                }

                case MacCG.EventLeftMouseDown:
                    _state.SetMouse(true, true);
                    break;

                case MacCG.EventLeftMouseUp:
                    _state.SetMouse(true, false);
                    break;

                case MacCG.EventRightMouseDown:
                    _state.SetMouse(false, true);
                    break;

                case MacCG.EventRightMouseUp:
                    _state.SetMouse(false, false);
                    break;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _state.Clear();

            IntPtr runLoop = _runLoop;

            if (runLoop != IntPtr.Zero)
                MacCF.CFRunLoopStop(runLoop);

            if (_thread is not null && !_thread.Join(TimeSpan.FromSeconds(2)))
                App.Logger.Warn("Key overlay event tap did not stop in time");

            _thread = null;
            _runLoop = IntPtr.Zero;

            ReleaseTap();
            FreeHandle();

            GC.SuppressFinalize(this);
        }
    }
}