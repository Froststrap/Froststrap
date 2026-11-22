// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;

namespace Froststrap.Models.Overlay
{
    internal sealed record OverlayBounds(Rect Rect, bool IsMaximised, double CornerRadius = 8);
}