// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.Overlay
{
    /// <summary>
    /// A rectangle in physical pixels, expressed relative to the overlay window.
    /// </summary>
    internal readonly record struct OverlayRegion(int Left, int Top, int Right, int Bottom);
}
