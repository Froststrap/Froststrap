// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Extensions
{
    static class KeyOverlayScaleEx
    {
        public static double GetFactor(this KeyOverlayScale keyOverlayScale) => keyOverlayScale switch
        {
            KeyOverlayScale.X85 => 0.85,
            KeyOverlayScale.X95 => 0.95,
            KeyOverlayScale.X115 => 1.15,
            KeyOverlayScale.X125 => 1.25,
            KeyOverlayScale.X135 => 1.35,
            KeyOverlayScale.X150 => 1.5,
            _ => 1.0
        };

        public static IReadOnlyCollection<KeyOverlayScale> Selections =>
        [
            KeyOverlayScale.X85,
            KeyOverlayScale.X95,
            KeyOverlayScale.X100,
            KeyOverlayScale.X115,
            KeyOverlayScale.X125,
            KeyOverlayScale.X135,
            KeyOverlayScale.X150
        ];
    }
}
