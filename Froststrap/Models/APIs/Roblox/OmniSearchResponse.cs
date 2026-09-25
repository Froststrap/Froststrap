// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox;

internal class OmniSearchResponse
{
    [JsonPropertyName("searchResults")]
    public List<OmniSearchGroup>? SearchResults { get; set; }
}

internal class OmniSearchGroup
{
    [JsonPropertyName("contentGroupType")]
    public string ContentGroupType { get; set; } = String.Empty;

    [JsonPropertyName("contents")]
    public List<OmniSearchContent>? Contents { get; set; }
}