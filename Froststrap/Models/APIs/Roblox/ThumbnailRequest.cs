namespace Froststrap.Models.APIs.Roblox
{
    internal class ThumbnailRequest
    {
        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }

        [JsonPropertyName("targetId")]
        public ulong TargetId { get; set; }

        /// <summary>
        /// List of valid types can be found at https://thumbnails.roblox.com//docs/index.html
        /// </summary>
        [JsonPropertyName("type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ThumbnailType Type { get; set; } = ThumbnailType.Avatar;

        /// <summary>
        /// TODO: Make it an Enum
        /// List of valid sizes can be found at https://thumbnails.roblox.com//docs/index.html
        /// </summary>
        [JsonPropertyName("size")]
        public string Size { get; set; } = "30x30";

        /// <summary>
        /// List of valid types can be found at https://thumbnails.roblox.com//docs/index.html
        /// </summary>
        [JsonPropertyName("format")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ThumbnailFormat Format { get; set; } = ThumbnailFormat.Png;

        [JsonPropertyName("isCircular")]
        public bool IsCircular { get; set; } = false;
    }
}