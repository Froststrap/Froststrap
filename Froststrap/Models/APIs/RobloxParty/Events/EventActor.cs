namespace Froststrap.Models.APIs.RobloxParty.Events
{
    internal class EventActor
    {
        [JsonPropertyName("Type")]
        public string Type { get; set; } = String.Empty;

        [JsonPropertyName("Id")]
        public string Id { get; set; } = String.Empty;

        public long UserId => long.TryParse(Id, out long id) ? id : 0;
    }
}
