// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;

namespace Froststrap.Models;

internal class NewsFile
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;
    [JsonPropertyName("news")] public List<NewsEntry> News { get; set; } = new();
}

internal class NewsEntry
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("pinned")] public bool Pinned { get; set; }
    [JsonPropertyName("timestamp")] public long Timestamp { get; set; }
    [JsonPropertyName("expires")] public long? Expires { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("image")] public string? Image { get; set; }
    [JsonPropertyName("buttons")] public List<NewsButton> Buttons { get; set; } = new();
    [JsonPropertyName("minVersion")] public string? MinVersion { get; set; }
    [JsonPropertyName("maxVersion")] public string? MaxVersion { get; set; }
    [JsonPropertyName("platforms")] public List<string> Platforms { get; set; } = new();

    public bool HasButtons => Buttons.Count > 0;
}

internal class NewsButton
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("link")] public string Link { get; set; } = "";
    [JsonPropertyName("primary")] public bool Primary { get; set; }
}