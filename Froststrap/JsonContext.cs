// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Text.Json.Serialization;
using Froststrap.UI.Elements.Editor;

namespace Froststrap
{
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString)]
    [JsonSerializable(typeof(Settings))]
    [JsonSerializable(typeof(State))]
    [JsonSerializable(typeof(DistributionState))]
    [JsonSerializable(typeof(RobloxState))]
    [JsonSerializable(typeof(RemoteDataBase))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(Dictionary<string, string[]>))]
    [JsonSerializable(typeof(List<object>))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(JsonDocument))]
    [JsonSerializable(typeof(GithubRelease))]
    [JsonSerializable(typeof(List<GithubRelease>))]
    [JsonSerializable(typeof(WebView2LatestResponse))]
    [JsonSerializable(typeof(WebView2ContentId))]
    [JsonSerializable(typeof(WebView2Download))]
    [JsonSerializable(typeof(List<WebView2Download>))]
    [JsonSerializable(typeof(FontFamily))]
    [JsonSerializable(typeof(FontFace))]
    [JsonSerializable(typeof(RobloxCookies))]
    [JsonSerializable(typeof(AuthenticatedUser))]
    [JsonSerializable(typeof(AccountManagerData))]
    [JsonSerializable(typeof(ApiArrayResponse<ThumbnailResponse>))]
    [JsonSerializable(typeof(ApiArrayResponse<GameDetailResponse>))]
    [JsonSerializable(typeof(UserPresenceResponse))]
    [JsonSerializable(typeof(UserPresence))]
    [JsonSerializable(typeof(Message))]
    [JsonSerializable(typeof(RichPresence))]
    [JsonSerializable(typeof(StudioMessage))]
    [JsonSerializable(typeof(StudioRichPresence))]
    [JsonSerializable(typeof(GameHistoryEntry))]
    [JsonSerializable(typeof(List<GameHistoryEntry>))]
    [JsonSerializable(typeof(ServerInfo))]
    [JsonSerializable(typeof(ClientVersion))]
    [JsonSerializable(typeof(RoValraRegionResponse))]
    [JsonSerializable(typeof(IPInfoResponse))]
    [JsonSerializable(typeof(DatacenterEntry))]
    [JsonSerializable(typeof(List<DatacenterEntry>))]
    [JsonSerializable(typeof(DatacentersCache))]
    [JsonSerializable(typeof(OmniSearchResponse))]
    [JsonSerializable(typeof(GetUserResponse))]
    [JsonSerializable(typeof(UserChannel))]
    [JsonSerializable(typeof(PrivateServersResponse))]
    [JsonSerializable(typeof(SubplacesResponse))]
    [JsonSerializable(typeof(SearchLandingResponse))]
    [JsonSerializable(typeof(FavoriteGamesResponse))]
    [JsonSerializable(typeof(OmniRecommendationResponse))]
    [JsonSerializable(typeof(ThumbnailBatchResponse))]
    [JsonSerializable(typeof(ThumbnailRequest))]
    [JsonSerializable(typeof(List<ThumbnailRequest>))]
    [JsonSerializable(typeof(ThumbnailResponse))]
    [JsonSerializable(typeof(GameDetailResponse))]
    [JsonSerializable(typeof(GradientStops))]
    [JsonSerializable(typeof(GradientExport))]
    [JsonSerializable(typeof(ModGeneratorInfo))]
    [JsonSerializable(typeof(VersionCacheEntry))]
    [JsonSerializable(typeof(StoredCredential))]
    [JsonSerializable(typeof(LoginCodeRequest))]
    [JsonSerializable(typeof(LoginTokenRequest))]
    [JsonSerializable(typeof(PresenceBatchRequest))]
    [JsonSerializable(typeof(JoinGameInstanceRequest))]
    [JsonSerializable(typeof(OmniRecommendationRequest))]
    [JsonSerializable(typeof(BootstrapperEditorWindow.CustomBootstrapperSchema.Schema))]
    internal partial class JsonContext : JsonSerializerContext
    {
    }
}
