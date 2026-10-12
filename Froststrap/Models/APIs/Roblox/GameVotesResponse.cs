// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal class GameVotesResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("upVotes")]
        public long UpVotes { get; set; }

        [JsonPropertyName("downVotes")]
        public long DownVotes { get; set; }
    }
}