using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Froststrap.Integrations
{
    internal sealed class Softkey : IDisposable
    {
        private uint _keyUp;
        private uint _keyLeft;
        private uint _keyDown;
        private uint _keyRight;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int LLKHF_INJECTED = 0x10;
        private const nuint InjectedMarker = 0x46535354;

        private enum ActiveHorizontal { None, Left, Right }
        private enum ActiveVertical { None, Up, Down }

        private readonly int _robloxPid;

        private Thread? _hookThread;
        private uint _hookThreadId;
        private IntPtr _hookHandle = IntPtr.Zero;
        private LowLevelKeyboardProc? _hookProc;

        private bool _physLeft;
        private bool _physRight;
        private ActiveHorizontal _activeHorizontal = ActiveHorizontal.None;

        private bool _physUp;
        private bool _physDown;
        private ActiveVertical _activeVertical = ActiveVertical.None;

        private bool _isEnabled = true;
        private bool _isDisposed;

        public Softkey(int robloxPid)
        {
            _robloxPid = robloxPid;

            AssignKeys(App.Settings.Prop.SoftKeyProfile);

            if (!OperatingSystem.IsWindows())
            {
                App.Logger.Warn("Softkey is only supported on Windows, skipping");
                return;
            }

            Start();
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            if (!enabled)
            {
                ClearState();
            }
        }

        private void AssignKeys(SoftKeyProfile profile)
        {
            switch (profile)
            {
                case SoftKeyProfile.AZERTY:
                    _keyUp = 0x5A;
                    _keyLeft = 0x51;
                    _keyDown = 0x53;
                    _keyRight = 0x44;
                    break;
                case SoftKeyProfile.ESDF:
                    _keyUp = 0x45;
                    _keyLeft = 0x53;
                    _keyDown = 0x44;
                    _keyRight = 0x46;
                    break;
                case SoftKeyProfile.ArrowKeys:
                    _keyUp = 0x26;
                    _keyLeft = 0x25;
                    _keyDown = 0x28;
                    _keyRight = 0x27;
                    break;
                case SoftKeyProfile.WASD:
                default:
                    _keyUp = 0x57;
                    _keyLeft = 0x41;
                    _keyDown = 0x53;
                    _keyRight = 0x44;
                    break;
            }
        }

        [SupportedOSPlatform("windows")]
        private void Start()
        {
            _hookThread = new Thread(HookThreadProc)
            {
                IsBackground = true,
                Name = "Softkey Hook"
            };
            _hookThread.Start();
        }

        [SupportedOSPlatform("windows")]
        private void HookThreadProc()
        {
            _hookThreadId = GetCurrentThreadId();
            _hookProc = HookCallback;

            _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, IntPtr.Zero, 0);

            if (_hookHandle == IntPtr.Zero)
            {
                App.Logger.Error($"Failed to install keyboard hook, error {Marshal.GetLastWin32Error()}");
                return;
            }

            App.Logger.Info("Keyboard hook installed");

            while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        [SupportedOSPlatform("windows")]
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode < 0 || !_isEnabled)
                    return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                bool isOurs = kb.dwExtraInfo == InjectedMarker || (kb.flags & LLKHF_INJECTED) != 0;
                if (isOurs)
                    return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

                IntPtr fgWindow = GetForegroundWindow();
                _ = GetWindowThreadProcessId(fgWindow, out uint fgPid);

                if (fgPid != _robloxPid)
                {
                    ClearState();
                    return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
                }

                bool isRelevantKey = kb.vkCode == _keyUp || kb.vkCode == _keyDown || kb.vkCode == _keyLeft || kb.vkCode == _keyRight;
                if (!isRelevantKey)
                    return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

                int msg = wParam.ToInt32();
                bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

                if (!isDown && !isUp)
                    return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

                if (kb.vkCode == _keyLeft || kb.vkCode == _keyRight)
                {
                    if (isDown) HandleKeyDownHorizontal(kb.vkCode == _keyLeft);
                    else HandleKeyUpHorizontal(kb.vkCode == _keyLeft);
                }
                else if (kb.vkCode == _keyUp || kb.vkCode == _keyDown)
                {
                    if (isDown) HandleKeyDownVertical(kb.vkCode == _keyUp);
                    else HandleKeyUpVertical(kb.vkCode == _keyUp);
                }

                return (IntPtr)1;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Hook callback crashed: {ex.Message}");
                return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }
        }

        private void HandleKeyDownHorizontal(bool isLeft)
        {
            if (isLeft)
            {
                if (_physLeft) return;
                _physLeft = true;

                if (_activeHorizontal != ActiveHorizontal.Left)
                {
                    if (_activeHorizontal == ActiveHorizontal.Right)
                        SendKey((int)_keyRight, false);

                    SendKey((int)_keyLeft, true);
                    _activeHorizontal = ActiveHorizontal.Left;
                }
            }
            else
            {
                if (_physRight) return;
                _physRight = true;

                if (_activeHorizontal != ActiveHorizontal.Right)
                {
                    if (_activeHorizontal == ActiveHorizontal.Left)
                        SendKey((int)_keyLeft, false);

                    SendKey((int)_keyRight, true);
                    _activeHorizontal = ActiveHorizontal.Right;
                }
            }
        }

        private void HandleKeyUpHorizontal(bool isLeft)
        {
            if (isLeft)
            {
                _physLeft = false;

                if (_activeHorizontal == ActiveHorizontal.Left)
                {
                    SendKey((int)_keyLeft, false);
                    _activeHorizontal = ActiveHorizontal.None;

                    if (_physRight)
                    {
                        SendKey((int)_keyRight, true);
                        _activeHorizontal = ActiveHorizontal.Right;
                    }
                }
            }
            else
            {
                _physRight = false;

                if (_activeHorizontal == ActiveHorizontal.Right)
                {
                    SendKey((int)_keyRight, false);
                    _activeHorizontal = ActiveHorizontal.None;

                    if (_physLeft)
                    {
                        SendKey((int)_keyLeft, true);
                        _activeHorizontal = ActiveHorizontal.Left;
                    }
                }
            }
        }

        private void HandleKeyDownVertical(bool isUp)
        {
            if (isUp)
            {
                if (_physUp) return;
                _physUp = true;

                if (_activeVertical != ActiveVertical.Up)
                {
                    if (_activeVertical == ActiveVertical.Down)
                        SendKey((int)_keyDown, false);

                    SendKey((int)_keyUp, true);
                    _activeVertical = ActiveVertical.Up;
                }
            }
            else
            {
                if (_physDown) return;
                _physDown = true;

                if (_activeVertical != ActiveVertical.Down)
                {
                    if (_activeVertical == ActiveVertical.Up)
                        SendKey((int)_keyUp, false);

                    SendKey((int)_keyDown, true);
                    _activeVertical = ActiveVertical.Down;
                }
            }
        }

        private void HandleKeyUpVertical(bool isUp)
        {
            if (isUp)
            {
                _physUp = false;

                if (_activeVertical == ActiveVertical.Up)
                {
                    SendKey((int)_keyUp, false);
                    _activeVertical = ActiveVertical.None;

                    if (_physDown)
                    {
                        SendKey((int)_keyDown, true);
                        _activeVertical = ActiveVertical.Down;
                    }
                }
            }
            else
            {
                _physDown = false;

                if (_activeVertical == ActiveVertical.Down)
                {
                    SendKey((int)_keyDown, false);
                    _activeVertical = ActiveVertical.None;

                    if (_physUp)
                    {
                        SendKey((int)_keyUp, true);
                        _activeVertical = ActiveVertical.Up;
                    }
                }
            }
        }

        private void ClearState()
        {
            if (_activeHorizontal == ActiveHorizontal.Left) SendKey((int)_keyLeft, false);
            else if (_activeHorizontal == ActiveHorizontal.Right) SendKey((int)_keyRight, false);

            if (_activeVertical == ActiveVertical.Up) SendKey((int)_keyUp, false);
            else if (_activeVertical == ActiveVertical.Down) SendKey((int)_keyDown, false);

            _physLeft = false;
            _physRight = false;
            _physUp = false;
            _physDown = false;

            _activeHorizontal = ActiveHorizontal.None;
            _activeVertical = ActiveVertical.None;
        }

        [SupportedOSPlatform("windows")]
        private static void SendKey(int vkCode, bool keyDown)
        {
            ushort scanCode = (ushort)MapVirtualKey((uint)vkCode, MAPVK_VK_TO_VSC);

            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)vkCode,
                        wScan = scanCode,
                        dwFlags = keyDown ? KEYEVENTF_SCANCODE : (KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP),
                        time = 0,
                        dwExtraInfo = InjectedMarker
                    }
                }
            };

            uint result = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());

            if (result == 0)
            {
                App.Logger.Warn($"SendInput failed (error {Marshal.GetLastWin32Error()}) – falling back to keybd_event.");

                byte bVk = (byte)vkCode;
                uint dwFlags = keyDown ? 0 : KEYEVENTF_KEYUP;
                keybd_event(bVk, (byte)scanCode, dwFlags, UIntPtr.Zero);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (OperatingSystem.IsWindows())
            {
                ClearState();

                if (_hookThreadId != 0)
                {
                    PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                    _hookThread?.Join(TimeSpan.FromSeconds(2));
                }

                if (_hookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookHandle);
                    _hookHandle = IntPtr.Zero;
                }

                App.Logger.Info("Keyboard hook removed");
            }
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint MAPVK_VK_TO_VSC = 0;
        private const uint WM_QUIT = 0x0012;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

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
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;

            [FieldOffset(0)]
            public KEYBDINPUT ki;

            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public nuint dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public nuint dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
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

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern uint GetCurrentThreadId();

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    }
}
