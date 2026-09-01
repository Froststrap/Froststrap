namespace Froststrap.Models.APIs.Roblox
{
    /// <summary>
    /// Roblox.Web.WebAPI.Models.ApiArrayResponse
    /// </summary>
    internal class ApiArrayResponse<T>
    {
        [JsonPropertyName("data")]
        public IEnumerable<T> Data { get; set; } = null!;
    }
}
