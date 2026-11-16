// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox;

internal class SearchLandingResponse
{
    [JsonPropertyName("sorts")]
    public List<SearchLandingSort>? Sorts { get; set; }
}

internal class SearchLandingSort
{
    [JsonPropertyName("sortId")]
    public string? SortId { get; set; }

    [JsonPropertyName("games")]
    public List<SearchLandingGame>? Games { get; set; }
}

internal class SearchLandingGame
{
    [JsonPropertyName("universeId")]
    public long UniverseId { get; set; }

    [JsonPropertyName("rootPlaceId")]
    public long RootPlaceId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("playerCount")]
    public int PlayerCount { get; set; }
}