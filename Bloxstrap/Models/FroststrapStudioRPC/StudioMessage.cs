namespace Bloxstrap.Models.FroststrapStudioRPC;

public class StudioMessage
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "SetRichPresence";

    [JsonPropertyName("data")]
    public StudioRichPresence Data { get; set; } = new();
}
