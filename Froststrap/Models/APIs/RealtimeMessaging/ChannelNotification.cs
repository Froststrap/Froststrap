using Froststrap.Models.APIs.RobloxParty.Events;

namespace Froststrap.Models.APIs.RealtimeMessaging
{
    internal class ChannelMarkedRead
    {
        [JsonPropertyName("Type")]
        public string Type { get; set; } = String.Empty;

        [JsonPropertyName("Actor")]
        public EventActor? Actor { get; set; }

        [JsonPropertyName("ChannelId")]
        public string ChannelId { get; set; } = String.Empty;

        [JsonPropertyName("ChannelType")]
        public string? ChannelType { get; set; }

        [JsonPropertyName("ChannelVertical")]
        public string? ChannelVertical { get; set; }
    }

    internal class ChannelMetadataUpdate
    {
        public const string UnreadMetadataType = "ChannelUnread";

        [JsonPropertyName("Type")]
        public string Type { get; set; } = String.Empty;

        [JsonPropertyName("MetadataUpdateType")]
        public string? MetadataUpdateType { get; set; }

        [JsonPropertyName("Actor")]
        public EventActor? Actor { get; set; }

        [JsonPropertyName("ChannelId")]
        public string ChannelId { get; set; } = String.Empty;

        [JsonPropertyName("ChannelType")]
        public string? ChannelType { get; set; }

        [JsonPropertyName("ChannelVertical")]
        public string? ChannelVertical { get; set; }
    }
}