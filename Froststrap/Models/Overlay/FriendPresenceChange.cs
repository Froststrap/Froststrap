// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Enums.Overlay;

namespace Froststrap.Models.Overlay
{
    internal record FriendPresenceChange(
        long UserId,
        FriendPresenceKind Kind,
        string GameName,
        long PlaceId = 0,
        long RootPlaceId = 0,
        string? ServerId = null);
}