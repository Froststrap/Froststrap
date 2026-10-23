// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Enums;

namespace Froststrap.Models.Overlay
{
    internal static class SoftKeyLayout
    {
        public static (uint Up, uint Left, uint Down, uint Right) Resolve(SoftKeyProfile profile) => profile switch
        {
            SoftKeyProfile.AZERTY => (0x5A, 0x51, 0x53, 0x44),
            SoftKeyProfile.ESDF => (0x45, 0x53, 0x44, 0x46),
            SoftKeyProfile.ArrowKeys => (0x26, 0x25, 0x28, 0x27),
            _ => (0x57, 0x41, 0x53, 0x44)
        };

        public static (string Up, string Left, string Down, string Right) Labels(SoftKeyProfile profile) => profile switch
        {
            SoftKeyProfile.AZERTY => ("Z", "Q", "S", "D"),
            SoftKeyProfile.ESDF => ("E", "S", "D", "F"),
            SoftKeyProfile.ArrowKeys => ("↑", "←", "↓", "→"),
            _ => ("W", "A", "S", "D")
        };
    }
}
