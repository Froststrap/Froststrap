// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox;

internal sealed class OmniSearchResponse
{
    [JsonPropertyName("searchResults")]
    public List<OmniSearchGroup>? SearchResults { get; set; }
}

internal sealed class OmniSearchGroup
{
    [JsonPropertyName("contents")]
    public List<OmniSearchContent>? Contents { get; set; }
}