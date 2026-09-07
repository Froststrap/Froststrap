// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;

namespace Froststrap.Models.APIs.Roblox;

internal class OmniSearchContent
{
    [JsonPropertyName("universeId")]
    public ulong UniverseId { get; set; }

    [JsonPropertyName("rootPlaceId")]
    public long RootPlaceId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("playerCount")]
    public int? PlayerCount { get; set; }

    private string? _thumbnailUrl;
    public string? ThumbnailUrl
    {
        get => _thumbnailUrl;
        set => _thumbnailUrl = value;
    }

    private Bitmap? _thumbnailBitmap;
    public Bitmap? ThumbnailBitmap
    {
        get => _thumbnailBitmap;
        set => _thumbnailBitmap = value;
    }
}

internal class FavoriteGamesResponse
{
    [JsonPropertyName("previousPageCursor")]
    public string? PreviousPageCursor { get; set; }

    [JsonPropertyName("nextPageCursor")]
    public string? NextPageCursor { get; set; }

    [JsonPropertyName("data")]
    public List<FavoriteGameData>? Data { get; set; }
}

internal class FavoriteGameData
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("creator")]
    public CreatorData? Creator { get; set; }

    [JsonPropertyName("rootPlace")]
    public RootPlaceData? RootPlace { get; set; }

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("updated")]
    public DateTime Updated { get; set; }

    [JsonPropertyName("placeVisits")]
    public long PlaceVisits { get; set; }
}

internal class CreatorData
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal class RootPlaceData
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}