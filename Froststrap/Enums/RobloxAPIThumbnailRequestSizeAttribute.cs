namespace Froststrap.Enums;

using System.Reflection;
using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter<ThumbnailSize>))]
internal enum ThumbnailSize
{
    [JsonStringEnumMemberName("30x30")]
    Small,
    [JsonStringEnumMemberName("48x48")]
    BitBigger,
    [JsonStringEnumMemberName("60x60")]
    Medium,
    [JsonStringEnumMemberName("128x128")]
    Large,
    [JsonStringEnumMemberName("150x150")]
    Detailed,
    [JsonStringEnumMemberName("512x512")]
    Cyberpunk,
}

internal static class ThumbnailSizeExtensions
{
    private static readonly Dictionary<string, ThumbnailSize> BySize = typeof(ThumbnailSize)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .ToDictionary(
            f => f.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? f.Name,
            f => (ThumbnailSize)f.GetValue(null)!);

    public static ThumbnailSize ToThumbnailSize(this string s) =>
        BySize.TryGetValue(s, out var size)
            ? size
            : throw new ArgumentException($"Unknown thumbnail size '{s}'", nameof(s));
}
