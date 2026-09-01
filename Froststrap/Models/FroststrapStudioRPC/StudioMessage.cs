namespace Froststrap.Models.FroststrapStudioRPC;

internal class StudioMessage
{
    [JsonPropertyName("command")]
    public string StudioCommand { get; set; } = null!;

    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }
}
