namespace Froststrap.Models
{
    public class AppStorageSettings
    {
        [JsonPropertyName("IsFirstLaunchAfterInstall")]
        public string IsFirstLaunchAfterInstall { get; set; } = "false";

        [JsonPropertyName("GamepadInteractionMoreRecentThanKeyboardMouse")]
        public string GamepadInteractionMoreRecentThanKeyboardMouse { get; set; } = "";

        [JsonPropertyName("WinInstallerDiceRoll")]
        public string WinInstallerDiceRoll { get; set; } = "0";

        [JsonPropertyName("EnableWidgetExtension")]
        public string EnableWidgetExtension { get; set; } = "false";

        [JsonPropertyName("PlayerExeLaunchTime")]
        public string PlayerExeLaunchTime { get; set; } = "";

        [JsonPropertyName("EnableLockScreenWidgetExperiment")]
        public string EnableLockScreenWidgetExperiment { get; set; } = "false";

        [JsonPropertyName("EngineStabilityStats")]
        public string EngineStabilityStats { get; set; } = "";

        [JsonPropertyName("AppInstallationId")]
        public string AppInstallationId { get; set; } = "";

        [JsonPropertyName("BrowserTrackerId")]
        public string BrowserTrackerId { get; set; } = "";

        [JsonPropertyName("RobloxLocaleId")]
        public string RobloxLocaleId { get; set; } = "en_us";

        [JsonPropertyName("UpdateControllerCacheJsonPayload")]
        public string UpdateControllerCacheJsonPayload { get; set; } = "";

        [JsonPropertyName("AppConfiguration")]
        public string AppConfiguration { get; set; } = "";

        [JsonPropertyName("WebLogin")]
        public string WebLogin { get; set; } = "";

        [JsonPropertyName("ExperimentCache")]
        public string ExperimentCache { get; set; } = "";

        [JsonPropertyName("PlayerHydrationBlob")]
        public string PlayerHydrationBlob { get; set; } = "";

        [JsonPropertyName("PlayerHydrationSignature")]
        public string PlayerHydrationSignature { get; set; } = "";

        [JsonPropertyName("PolicyServiceHttpResponse")]
        public string PolicyServiceHttpResponse { get; set; } = "";

        [JsonPropertyName("ActivePartyId")]
        public string ActivePartyId { get; set; } = "";

        [JsonPropertyName("SessionWithKeyboardAndMouse")]
        public string SessionWithKeyboardAndMouse { get; set; } = "";

        [JsonPropertyName("NativeCloseLuaPromptDisplayCount")]
        public string NativeCloseLuaPromptDisplayCount { get; set; } = "0";

        [JsonPropertyName("UserId")]
        public string UserId { get; set; } = "0";

        [JsonPropertyName("UpdateControllerCacheChannel")]
        public string UpdateControllerCacheChannel { get; set; } = "";

        [JsonPropertyName("Username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("Membership")]
        public string Membership { get; set; } = "";

        [JsonPropertyName("IsUnder13")]
        public string IsUnder13 { get; set; } = "false";

        [JsonPropertyName("DisplayName")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("CountryCode")]
        public string CountryCode { get; set; } = "";

        [JsonPropertyName("WebViewUserAgent")]
        public string WebViewUserAgent { get; set; } = "";

        [JsonPropertyName("PreviousAccountsList")]
        public string PreviousAccountsList { get; set; } = "[]";

        [JsonPropertyName("DeviceLevelThemeSnapshotTimestamp")]
        public string DeviceLevelThemeSnapshotTimestamp { get; set; } = "";

        [JsonPropertyName("DeviceLevelTheme")]
        public string DeviceLevelTheme { get; set; } = "{\"0\":\"dark\"}";

        [JsonPropertyName("GameLocaleId")]
        public string GameLocaleId { get; set; } = "en_us";

        [JsonPropertyName("AuthenticatedTheme")]
        public string AuthenticatedTheme { get; set; } = "";

        [JsonPropertyName("ExperienceMenuVersion")]
        public string ExperienceMenuVersion { get; set; } = "";

        [JsonPropertyName("DiscoveryClientFallbackCache")]
        public string DiscoveryClientFallbackCache { get; set; } = "";

        [JsonPropertyName("WebViewUserAgentCacheTime")]
        public string WebViewUserAgentCacheTime { get; set; } = "";

        [JsonPropertyName("InGameMenuState")]
        public string InGameMenuState { get; set; } = "";

        [JsonPropertyName("UpdateControllerCacheTimestamp")]
        public string UpdateControllerCacheTimestamp { get; set; } = "";

        [JsonPropertyName("LaunchAtStartup")]
        public string LaunchAtStartup { get; set; } = "false";

        [JsonPropertyName("SystemTrayModalShown")]
        public string SystemTrayModalShown { get; set; } = "false";

        [JsonPropertyName("MinimizeToTray")]
        public string MinimizeToTray { get; set; } = "false";
    }
}