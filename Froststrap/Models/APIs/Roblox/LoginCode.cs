namespace Froststrap.Models.APIs.Roblox
{
    internal sealed class LoginCodeRequest
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = "";

        [JsonPropertyName("privateKey")]
        public string PrivateKey { get; set; } = "";
    }

    internal sealed class LoginTokenRequest
    {
        [JsonPropertyName("ctype")]
        public string Ctype { get; set; } = "AuthToken";

        [JsonPropertyName("cvalue")]
        public string Cvalue { get; set; } = "";

        [JsonPropertyName("password")]
        public string Password { get; set; } = "";
    }
}