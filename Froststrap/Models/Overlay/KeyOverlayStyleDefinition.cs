// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media;

using Froststrap.Enums;

namespace Froststrap.Models.Overlay
{
    internal sealed record KeyOverlayStyleDefinition(
        KeyOverlayLayout Layout,
        Color Card,
        Color Key,
        Color KeyText,
        Color Divider,
        Color Label);
}
