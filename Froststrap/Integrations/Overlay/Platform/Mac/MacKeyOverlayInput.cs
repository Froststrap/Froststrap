// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Froststrap.Enums;
using Froststrap.Models.Overlay;

namespace Froststrap.Integrations.Overlay.Platform.Mac
{
    internal sealed class MacKeyOverlayInput : IKeyOverlayInput
    {
        private const uint SpaceKeyCode = 0x31;
        private const double CpsTimeConstantSeconds = 1.0;

        private const ulong EventMask =
            (1UL << (int)MacCG.EventKeyDown)
            | (1UL << (int)MacCG.EventKeyUp)
            | (1UL << (int)MacCG.EventFlagsChanged)
            | (1UL << (int)MacCG.EventLeftMouseDown)
            | (1UL << (int)MacCG.EventLeftMouseUp)
            | (1UL << (int)MacCG.EventRightMouseDown)
            | (1UL << (int)MacCG.EventRightMouseUp);

        private readonly Func<bool> _isGameForeground;
        private readonly uint _keyUp;
        private readonly uint _keyLeft;
        private readonly uint _keyDown;
        private readonly uint _keyRight;

        private readonly GCHandle _selfHandle;

        private Thread? _thread;
        private IntPtr _tap;
        private IntPtr _source;
        private IntPtr _runLoop;

        private volatile bool _up;
        private volatile bool _left;
        private volatile bool _down;
        private volatile bool _right;
        private volatile bool _space;
        private volatile bool _mouseLeft;
        private volatile bool _mouseRight;

        private readonly object _scoreLock = new();

        private double _leftScore;
        private double _rightScore;
        private long _scoreTimestamp;

        private volatile bool _isDisposed;
        private int _resourcesFreed;

        public MacKeyOverlayInput(int processId, SoftKeyProfile profile, Func<bool> isGameForeground)
        {
            _ = processId;
            _isGameForeground = isGameForeground;

            var keys = MacSoftKeyLayout.Resolve(profile);

            _keyUp = keys.Up;
            _keyLeft = keys.Left;
            _keyDown = keys.Down;
            _keyRight = keys.Right;

            _selfHandle = GCHandle.Alloc(this, GCHandleType.Normal);

            _thread = new Thread(TapThread)
            {
                IsBackground = true,
                Name = "Key Overlay Event Tap"
            };

            _thread.Start();
        }

        public KeyOverlaySnapshot GetSnapshot()
        {
            long now = Environment.TickCount64;

            double leftCps;
            double rightCps;

            lock (_scoreLock)
            {
                DecayScores(now);

                leftCps = _leftScore / CpsTimeConstantSeconds;
                rightCps = _rightScore / CpsTimeConstantSeconds;
            }

            return new KeyOverlaySnapshot(
                _up,
                _left,
                _down,
                _right,
                _space,
                _mouseLeft,
                _mouseRight,
                leftCps,
                rightCps);
        }

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
                ClearState();
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

                    UpdateKey(keyCode, type == MacCG.EventKeyDown);
                    break;
                }

                case MacCG.EventLeftMouseDown:
                    _mouseLeft = true;
                    AddClick(true);
                    break;

                case MacCG.EventLeftMouseUp:
                    _mouseLeft = false;
                    break;

                case MacCG.EventRightMouseDown:
                    _mouseRight = true;
                    AddClick(false);
                    break;

                case MacCG.EventRightMouseUp:
                    _mouseRight = false;
                    break;
            }
        }

        private void UpdateKey(uint keyCode, bool pressed)
        {
            if (keyCode == _keyUp) _up = pressed;
            else if (keyCode == _keyDown) _down = pressed;
            else if (keyCode == _keyLeft) _left = pressed;
            else if (keyCode == _keyRight) _right = pressed;
            else if (keyCode == SpaceKeyCode) _space = pressed;
        }

        private void DecayScores(long now)
        {
            if (_scoreTimestamp == 0)
            {
                _scoreTimestamp = now;
                return;
            }

            double elapsed = (now - _scoreTimestamp) / 1000.0;

            if (elapsed <= 0)
                return;

            double decay = Math.Exp(-elapsed / CpsTimeConstantSeconds);

            _leftScore *= decay;
            _rightScore *= decay;
            _scoreTimestamp = now;
        }

        private void AddClick(bool left)
        {
            long now = Environment.TickCount64;

            lock (_scoreLock)
            {
                DecayScores(now);

                if (left)
                    _leftScore += 1;
                else
                    _rightScore += 1;
            }
        }

        private void ClearState()
        {
            _up = false;
            _left = false;
            _down = false;
            _right = false;
            _space = false;
            _mouseLeft = false;
            _mouseRight = false;

            lock (_scoreLock)
            {
                _leftScore = 0;
                _rightScore = 0;
                _scoreTimestamp = 0;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            ClearState();

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