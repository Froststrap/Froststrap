namespace Froststrap.Models.APIs.Roblox
{
    internal sealed class OmniRecommendationRequest
    {
        [JsonPropertyName("pageType")]
        public string PageType { get; set; } = "Home";

        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = "";

        [JsonPropertyName("supportedTreatmentTypes")]
        public string[] SupportedTreatmentTypes { get; set; } = [];

        [JsonPropertyName("sduiTreatmentTypes")]
        public string[] SduiTreatmentTypes { get; set; } = [];

        [JsonPropertyName("cpuCores")]
        public int CpuCores { get; set; }

        [JsonPropertyName("maxResolution")]
        public string MaxResolution { get; set; } = "";

        [JsonPropertyName("maxMemory")]
        public int MaxMemory { get; set; }

        [JsonPropertyName("networkType")]
        public string NetworkType { get; set; } = "";
    }
}