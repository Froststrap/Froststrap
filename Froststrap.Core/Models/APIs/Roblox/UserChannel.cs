// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal class UserChannel
    {
        [JsonPropertyName("channelName")]
        public string Channel { get; set; } = "production";

        [JsonPropertyName("channelAssignmentType")]
        public int? AssignmentType { get; set; }

        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }
}