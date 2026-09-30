// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    /// <summary>
    /// List of valid types can be found at https://thumbnails.roblox.com//docs/index.html
    /// </summary>
    internal class ThumbnailRequest
    {
        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }

        [JsonPropertyName("targetId")]
        public ulong? TargetId { get; set; }

        [JsonPropertyName("token")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Token { get; set; }

        [JsonPropertyName("type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ThumbnailType Type { get; set; } = ThumbnailType.Avatar;

        [JsonPropertyName("size")]
        public ThumbnailSize Size { get; set; } = ThumbnailSize.Small;

        [JsonPropertyName("format")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ThumbnailFormat Format { get; set; } = ThumbnailFormat.Png;

        [JsonPropertyName("isCircular")]
        public bool IsCircular { get; set; } = false;
    }
}
