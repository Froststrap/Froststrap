using Froststrap.Models.RobloxApi;
using Froststrap.RobloxInterfaces;

namespace Froststrap.Models.Entities
{
    internal class UserDetails
    {
        private static List<UserDetails> Cache { get; set; } = [];

        public GetUserResponse Data { get; set; } = null!;

        public ThumbnailResponse Thumbnail { get; set; } = null!;

        public static async Task<UserDetails> Fetch(long id)
        {
            Uri userUrl = UrlBuilder.BuildApiUrl("users", "v1/users/" + id);
            Uri thumbnailsUrl = UrlBuilder.BuildApiUrl("thumbnails", $"v1/users/avatar-headshot?userIds={id}&size=180x180&format=Png&isCircular=false");

            var cached = Cache.FirstOrDefault(x => x.Data?.Id == id);
            if (cached != null)
                return cached;

            var userResponse = await Http.GetJson<GetUserResponse>(userUrl);
            _ = userResponse ?? throw new InvalidHTTPResponseException("Roblox API for User Details returned invalid data");

            var thumbnailResponse = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(thumbnailsUrl);
            if (thumbnailResponse is null || !thumbnailResponse.Data.Any())
                throw new InvalidHTTPResponseException("Roblox API for Thumbnails returned invalid data");

            var details = new UserDetails
            {
                Data = userResponse,
                Thumbnail = thumbnailResponse.Data.First()
            };

            Cache.Add(details);
            return details;
        }
    }
}