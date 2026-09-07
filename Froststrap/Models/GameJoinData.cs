// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models
{
    internal class GameJoinData
    {
        public GameJoinType JoinType = GameJoinType.Unknown;

        public long? PlaceId { get; set; }
        public string? JobId { get; set; }
        public long? UserId { get; set; }
        public string? JoinOrigin;
        public string? AccessCode { get; set; }
        public string PlaceLauncherUrl { get; set; } = string.Empty;
    }
}