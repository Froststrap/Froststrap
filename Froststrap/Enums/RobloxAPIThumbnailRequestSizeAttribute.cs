namespace Froststrap.Enums;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter<ThumbnailSize>))]
internal enum ThumbnailSize
{
    [JsonStringEnumMemberName("30x30")]
    Small,
    [JsonStringEnumMemberName("60x60")]
    Medium,
    [JsonStringEnumMemberName("128x128")]
    Large,
    [JsonStringEnumMemberName("150x150")]
    Detailed,
    [JsonStringEnumMemberName("512x512")]
    Cyberpunk,
}
