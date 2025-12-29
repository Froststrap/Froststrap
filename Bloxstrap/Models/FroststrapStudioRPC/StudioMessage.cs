namespace Bloxstrap.Models.FroststrapStudioRPC;

public class StudioMessage
{
    [JsonPropertyName("command")]
    public string StudioCommand { get; set; } = null!;

    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }
}
