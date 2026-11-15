// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Backend;
using Froststrap.Integrations.Overlay;

using System.Runtime.InteropServices;

namespace Froststrap.Integrations.Overlay.Platform.Linux
{
    internal sealed class LinuxKeyOverlayInput : IKeyOverlayInput
    {
        private const uint Space = 57;
        private const uint MouseLeft = 1;
        private const uint MouseRight = 2;

        private readonly IntPtr _state = Marshal.AllocHGlobal(LinuxOverlay.KeyStateSize);
        private readonly uint _up;
        private readonly uint _left;
        private readonly uint _down;
        private readonly uint _right;

        public LinuxKeyOverlayInput(SoftKeyProfile profile)
        {
            (_up, _left, _down, _right) = profile switch
            {
                SoftKeyProfile.ESDF => (18u, 31u, 32u, 33u),
                SoftKeyProfile.ArrowKeys => (103u, 105u, 108u, 106u),
                _ => (17u, 30u, 31u, 32u)
            };
        }

        public KeyOverlaySnapshot GetSnapshot()
        {
            if (!LinuxOverlay.GetKeyState(_state))
                return default;

            var state = Marshal.PtrToStructure<LinuxOverlay.KeyState>(_state);
            byte[] keys = state.Keys;

            return new KeyOverlaySnapshot(
                Pressed(keys, _up),
                Pressed(keys, _left),
                Pressed(keys, _down),
                Pressed(keys, _right),
                Pressed(keys, Space),
                (state.Mouse & MouseLeft) != 0,
                (state.Mouse & MouseRight) != 0,
                state.LeftCps,
                state.RightCps);
        }

        private static bool Pressed(byte[] keys, uint code)
        {
            int index = (int)(code / 8);

            if (keys is null || index < 0 || index >= keys.Length)
                return false;

            return (keys[index] & (1 << (int)(code % 8))) != 0;
        }

        public void Dispose() => Marshal.FreeHGlobal(_state);
    }
}