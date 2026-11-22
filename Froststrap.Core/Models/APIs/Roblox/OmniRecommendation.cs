// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox;

internal class OmniRecommendationResponse
{
    [JsonPropertyName("sorts")]
    public List<OmniSort>? Sorts { get; set; }

    [JsonPropertyName("contentMetadata")]
    public OmniContentMetadata? ContentMetadata { get; set; }
}

internal class OmniSort
{
    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("topicId")]
    public long TopicId { get; set; }

    [JsonPropertyName("treatmentType")]
    public string? TreatmentType { get; set; }

    [JsonPropertyName("recommendationList")]
    public List<OmniRecommendation>? RecommendationList { get; set; }
}

internal class OmniRecommendation
{
    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    [JsonPropertyName("contentId")]
    public long ContentId { get; set; }

    [JsonPropertyName("contentStringId")]
    public string? ContentStringId { get; set; }
}

internal class OmniContentMetadata
{
    [JsonPropertyName("Game")]
    public Dictionary<string, OmniGameDetails>? Game { get; set; }
}

internal class OmniGameDetails
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("universeId")]
    public long UniverseId { get; set; }

    [JsonPropertyName("rootPlaceId")]
    public long RootPlaceId { get; set; }

    [JsonPropertyName("playerCount")]
    public int PlayerCount { get; set; }

    [JsonPropertyName("totalUpVotes")]
    public int TotalUpVotes { get; set; }

    [JsonPropertyName("totalDownVotes")]
    public int TotalDownVotes { get; set; }
}
