// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Text.Json.Nodes;

namespace Froststrap.Models
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

    internal sealed class PresenceBatchRequest
    {
        [JsonPropertyName("userIds")]
        public long[] UserIds { get; set; } = [];
    }

    internal sealed class JoinGameInstanceRequest
    {
        [JsonPropertyName("placeId")]
        public long PlaceId { get; set; }

        [JsonPropertyName("isTeleport")]
        public bool IsTeleport { get; set; }

        [JsonPropertyName("gameId")]
        public string GameId { get; set; } = "";

        [JsonPropertyName("gameJoinAttemptId")]
        public string GameJoinAttemptId { get; set; } = "";
    }

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

    internal sealed class ModGeneratorInfo
    {
        public string FroststrapVersion { get; set; } = "";
        public string RobloxVersion { get; set; } = "";
        public string RobloxVersionHash { get; set; } = "";
        public JsonNode? ColorsUsed { get; set; }
    }

    internal sealed class VersionCacheEntry
    {
        public string Version { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }

    internal sealed class GradientExport
    {
        public List<GradientStops> GradientStops { get; set; } = [];
        public double? GradientAngle { get; set; }
    }

    internal sealed class StoredCredential
    {
        [JsonPropertyName("$type")]
        public string Type { get; set; } = "CredentialWithToken";

        [JsonPropertyName("Token")]
        public string Token { get; set; } = "";
    }
}
