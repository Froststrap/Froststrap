// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Enums;
using Froststrap.Models.Overlay;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Froststrap.Integrations.OverlayModules
{
    internal sealed class KeyOverlayInput : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;

        private const int LLKHF_INJECTED = 0x10;

        private const uint VkSpace = 0x20;

        private const double CpsTimeConstantSeconds = 1.0;

        private readonly int _robloxPid;

        private readonly uint _keyUp;
        private readonly uint _keyLeft;
        private readonly uint _keyDown;
        private readonly uint _keyRight;

        private Thread? _hookThread;
        private uint _hookThreadId;
        private IntPtr _keyboardHook = IntPtr.Zero;
        private IntPtr _mouseHook = IntPtr.Zero;
        private LowLevelKeyboardProc? _keyboardProc;
        private LowLevelMouseProc? _mouseProc;

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

        private bool _isDisposed;

        public KeyOverlayInput(int robloxPid, SoftKeyProfile profile)
        {
            _robloxPid = robloxPid;

            var keys = SoftKeyLayout.Resolve(profile);

            _keyUp = keys.Up;
            _keyLeft = keys.Left;
            _keyDown = keys.Down;
            _keyRight = keys.Right;

            if (!OperatingSystem.IsWindows())
            {
                App.Logger.Warn("Key overlay input is only supported on Windows, skipping");
                return;
            }

            Start();
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

        [SupportedOSPlatform("windows")]
        private void Start()
        {
            _hookThread = new Thread(HookThreadProc)
            {
                IsBackground = true,
                Name = "Key Overlay Hook"
            };
            _hookThread.Start();
        }

        [SupportedOSPlatform("windows")]
        private void HookThreadProc()
        {
            _hookThreadId = GetCurrentThreadId();
            _keyboardProc = KeyboardProc;
            _mouseProc = MouseProc;

            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, IntPtr.Zero, 0);

            if (_keyboardHook == IntPtr.Zero)
            {
                App.Logger.Error($"Key overlay failed to install the keyboard hook, error {Marshal.GetLastWin32Error()}");
                return;
            }

            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, IntPtr.Zero, 0);

            if (_mouseHook == IntPtr.Zero)
                App.Logger.Error($"Key overlay failed to install the mouse hook, error {Marshal.GetLastWin32Error()}");

            App.Logger.Info("Key overlay hooks installed");

            while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        [SupportedOSPlatform("windows")]
        private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode < 0)
                    return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                if ((kb.flags & LLKHF_INJECTED) != 0)
                    return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

                uint vkCode = kb.vkCode;

                if (vkCode != _keyUp && vkCode != _keyDown && vkCode != _keyLeft && vkCode != _keyRight && vkCode != VkSpace)
                    return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

                if (!IsGameForeground())
                {
                    ClearState();
                    return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                }

                int msg = wParam.ToInt32();
                bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

                if (isDown || isUp)
                    UpdateKey(vkCode, isDown);

                return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Key overlay keyboard hook crashed");
                return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }
        }

        [SupportedOSPlatform("windows")]
        private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode < 0)
                    return CallNextHookEx(_mouseHook, nCode, wParam, lParam);

                int msg = wParam.ToInt32();

                if (msg == WM_LBUTTONDOWN || msg == WM_LBUTTONUP || msg == WM_RBUTTONDOWN || msg == WM_RBUTTONUP)
                {
                    if (!IsGameForeground())
                    {
                        ClearState();
                        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
                    }

                    switch (msg)
                    {
                        case WM_LBUTTONDOWN:
                            _mouseLeft = true;
                            AddClick(true);
                            break;

                        case WM_LBUTTONUP:
                            _mouseLeft = false;
                            break;

                        case WM_RBUTTONDOWN:
                            _mouseRight = true;
                            AddClick(false);
                            break;

                        case WM_RBUTTONUP:
                            _mouseRight = false;
                            break;
                    }
                }

                return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Key overlay mouse hook crashed");
                return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
            }
        }

        private void UpdateKey(uint vkCode, bool pressed)
        {
            if (vkCode == _keyUp) _up = pressed;
            else if (vkCode == _keyDown) _down = pressed;
            else if (vkCode == _keyLeft) _left = pressed;
            else if (vkCode == _keyRight) _right = pressed;
            else if (vkCode == VkSpace) _space = pressed;
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

        [SupportedOSPlatform("windows")]
        private bool IsGameForeground()
        {
            IntPtr foreground = GetForegroundWindow();

            if (foreground == IntPtr.Zero)
                return false;

            _ = GetWindowThreadProcessId(foreground, out uint pid);

            return pid == _robloxPid;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            if (OperatingSystem.IsWindows())
            {
                ClearState();

                if (_hookThreadId != 0)
                {
                    PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                    _hookThread?.Join(TimeSpan.FromSeconds(2));
                }

                if (_keyboardHook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_keyboardHook);
                    _keyboardHook = IntPtr.Zero;
                }

                if (_mouseHook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHook);
                    _mouseHook = IntPtr.Zero;
                }

                App.Logger.Info("Key overlay hooks removed");
            }

            GC.SuppressFinalize(this);
        }

        private const uint WM_QUIT = 0x0012;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public nuint dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int pt_x;
            public int pt_y;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}
