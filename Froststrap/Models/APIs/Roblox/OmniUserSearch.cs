// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Text.Json.Serialization;

namespace Froststrap.Models.APIs.Roblox
{
    internal sealed class OmniUserSearchResponse
    {
        [JsonPropertyName("searchResults")]
        public List<OmniUserSearchGroup>? SearchResults { get; set; }
    }

    internal sealed class OmniUserSearchGroup
    {
        [JsonPropertyName("contentGroupType")]
        public string? ContentGroupType { get; set; }

        [JsonPropertyName("contents")]
        public List<OmniUserSearchContent>? Contents { get; set; }
    }

    internal sealed class OmniUserSearchContent
    {
        [JsonPropertyName("contentId")]
        public long ContentId { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("hasVerifiedBadge")]
        public bool HasVerifiedBadge { get; set; }

        [JsonPropertyName("contentType")]
        public string? ContentType { get; set; }
    }

    internal sealed class AvatarHeadshotResponse
    {
        [JsonPropertyName("targetId")]
        public long TargetId { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }
    }
}