using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Froststrap.RobloxInterfaces
{
    internal class Badges
    {
        private const int PageSize = 100;
        private const int MaxPages = 5;

        private const int RemoveAttempts = 3;

        private static readonly TimeSpan RateLimitBackoff = TimeSpan.FromSeconds(2);

        private static readonly Lock CacheLock = new();
        private static readonly TimeSpan ListTtl = TimeSpan.FromMinutes(5);

        private static readonly Dictionary<long, (List<BadgeResponse> Badges, DateTime At)> ListCache = [];
        private static readonly Dictionary<long, string> IconCache = [];

        public static async Task<List<Badge>> FetchAsync(long universeId, long userId)
        {
            var badges = new List<Badge>();

            if (universeId == 0)
                return badges;

            List<BadgeResponse> responses = await FetchListAsync(universeId);

            foreach (BadgeResponse badge in responses)
            {
                if (!badge.Enabled)
                    continue;

                badges.Add(new Badge
                {
                    Id = badge.Id,
                    Name = String.IsNullOrEmpty(badge.DisplayName) ? badge.Name : badge.DisplayName!,
                    Description = badge.DisplayDescription ?? badge.Description ?? String.Empty,
                    WinRatePercentage = badge.Statistics?.WinRatePercentage ?? 0,
                    PastDayAwardedCount = badge.Statistics?.PastDayAwardedCount ?? 0,
                    AwardedCount = badge.Statistics?.AwardedCount ?? 0
                });
            }

            await PopulateAwardedAsync(badges, userId);
            await PopulateIconsAsync(badges);

            return badges;
        }

        private static async Task<List<BadgeResponse>> FetchListAsync(long universeId)
        {
            lock (CacheLock)
            {
                if (ListCache.TryGetValue(universeId, out var cached) && DateTime.UtcNow - cached.At < ListTtl)
                    return cached.Badges;
            }

            var responses = new List<BadgeResponse>();
            string? cursor = null;

            for (int page = 0; page < MaxPages; page++)
            {
                var response = await Http.GetJson<ApiPageResponse<BadgeResponse>>(
                    UrlBuilder.BuildApiUrl("badges", $"v1/universes/{universeId}/badges?limit={PageSize}&sortOrder=Asc&cursor={cursor}"));

                if (response is null)
                    break;

                responses.AddRange(response.Data);

                cursor = response.NextPageCursor;

                if (String.IsNullOrEmpty(cursor))
                    break;
            }

            if (responses.Count > 0)
            {
                lock (CacheLock)
                    ListCache[universeId] = (responses, DateTime.UtcNow);
            }

            return responses;
        }

        private static async Task PopulateAwardedAsync(List<Badge> badges, long userId)
        {
            if (userId == 0 || badges.Count == 0)
                return;

            if (!await App.Cookies.EnsureLoadedAsync())
            {
                App.Logger.Info("No session to ask with, leaving badge progress unknown");
                return;
            }

            try
            {
                var awarded = await AwardedDatesAsync(userId, badges.Select(x => x.Id));

                foreach (Badge badge in badges)
                {
                    if (awarded.TryGetValue(badge.Id, out DateTime date))
                    {
                        badge.Awarded = true;
                        badge.AwardedDate = date;
                    }

                    badge.AwardedKnown = true;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to fetch awarded dates");
                App.Logger.Error(ex);
            }
        }

        public static async Task RemoveAsync(long badgeId)
        {
            Uri url = UrlBuilder.BuildApiUrl("badges", $"v1/user/badges/{badgeId}");

            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await AccountRequests.DeleteAsync(url);
                    return;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests && attempt < RemoveAttempts)
                {
                    await Task.Delay(RateLimitBackoff * attempt);
                }
            }
        }

        public static async Task<Dictionary<long, DateTime>> AwardedDatesAsync(long userId, IEnumerable<long> badgeIds)
        {
            var awarded = new Dictionary<long, DateTime>();

            foreach (long[] chunk in badgeIds.Chunk(PageSize))
            {
                var response = await Http.AuthGetJson<ApiArrayResponse<BadgeAwardedDate>>(
                    UrlBuilder.BuildApiUrl("badges", $"v1/users/{userId}/badges/awarded-dates?badgeIds={String.Join(',', chunk)}"));

                if (response?.Data is null)
                    continue;

                foreach (BadgeAwardedDate date in response.Data)
                    awarded[date.BadgeId] = date.AwardedDate;
            }

            return awarded;
        }

        private static async Task PopulateIconsAsync(List<Badge> badges)
        {
            if (badges.Count == 0)
                return;

            var missing = new List<Badge>();

            lock (CacheLock)
            {
                foreach (Badge badge in badges)
                {
                    if (IconCache.TryGetValue(badge.Id, out string? url))
                        badge.IconUrl = url;
                    else
                        missing.Add(badge);
                }
            }

            if (missing.Count == 0)
                return;

            try
            {
                foreach (long[] chunk in missing.Select(x => x.Id).Chunk(PageSize))
                {
                    var response = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(
                        UrlBuilder.BuildApiUrl("thumbnails", $"v1/badges/icons?badgeIds={String.Join(',', chunk)}&size=150x150&format=Png&isCircular=false"));

                    if (response?.Data is null)
                        continue;

                    lock (CacheLock)
                    {
                        foreach (ThumbnailResponse thumbnail in response.Data)
                        {
                            if (thumbnail.State != "Completed" || String.IsNullOrEmpty(thumbnail.ImageUrl))
                                continue;

                            IconCache[thumbnail.TargetId] = thumbnail.ImageUrl;

                            Badge? badge = badges.FirstOrDefault(x => x.Id == thumbnail.TargetId);
                            badge?.IconUrl = thumbnail.ImageUrl;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to fetch badge icons");
                App.Logger.Error(ex);
            }
        }
    }
}