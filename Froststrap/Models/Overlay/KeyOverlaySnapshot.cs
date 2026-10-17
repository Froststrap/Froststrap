// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.Overlay
{
    internal readonly record struct KeyOverlaySnapshot(
        bool Up,
        bool Left,
        bool Down,
        bool Right,
        bool Space,
        bool MouseLeft,
        bool MouseRight,
        double LeftCps,
        double RightCps);
}
