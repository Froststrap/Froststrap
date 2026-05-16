namespace Froststrap.Models.Persistable
{
    public class SoberSettings
    {
        [JsonPropertyName("allow_gamepad_permission")]
        public bool AllowGamepadPermission { get; set; } = false;

        [JsonPropertyName("close_on_leave")]
        public bool CloseOnLeave { get; set; } = false;

        [JsonPropertyName("discord_rpc_enabled")]
        public bool DiscordRpcEnabled { get; set; } = true;

        [JsonPropertyName("discord_rpc_show_join_button")]
        public bool DiscordRpcShowJoinButton { get; set; } = true;

        [JsonPropertyName("enable_gamemode")]
        public bool EnableGamemode { get; set; } = true;

        [JsonPropertyName("enable_hidpi")]
        public bool EnableHiDpi { get; set; } = false;

        [JsonPropertyName("fflags")]
        public Dictionary<string, object> FFlags { get; set; } = new()
        {
            { "FFlagHandleAltEnterFullscreenManually", false }
        };

        [JsonPropertyName("graphics_optimization_mode")]
        public string GraphicsOptimizationMode { get; set; } = "balanced";

        [JsonPropertyName("server_location_indicator_enabled")]
        public bool ServerLocationIndicatorEnabled { get; set; } = false;

        [JsonPropertyName("touch_mode")]
        public string TouchMode { get; set; } = "off";

        [JsonPropertyName("use_console_experience")]
        public bool UseConsoleExperience { get; set; } = false;

        [JsonPropertyName("use_libsecret")]
        public bool UseLibsecret { get; set; } = false;

        [JsonPropertyName("use_opengl")]
        public bool UseOpengl { get; set; } = false;
    }
}
