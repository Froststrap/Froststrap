using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Froststrap;

namespace Froststrap.RobloxInterfaces
{
    internal class Badges
    {
        private const int PageSize = 100;
        private const int MaxPages = 5;

        public static async Task<List<Badge>> FetchAsync(long universeId, long userId)
        {
            var badges = new List<Badge>();

            if (universeId == 0)
                return badges;

            string? cursor = null;

            for (int page = 0; page < MaxPages; page++)
            {
                var response = await Http.GetJson<ApiPageResponse<BadgeResponse>>(
                    UrlBuilder.BuildApiUrl("badges", $"v1/universes/{universeId}/badges?limit={PageSize}&sortOrder=Asc&cursor={cursor}"));

                if (response is null)
                    break;

                foreach (BadgeResponse badge in response.Data)
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

                cursor = response.NextPageCursor;

                if (String.IsNullOrEmpty(cursor))
                    break;
            }

            await PopulateAwardedAsync(badges, userId);
            await PopulateIconsAsync(badges);

            return badges;
        }

        private static async Task PopulateAwardedAsync(List<Badge> badges, long userId)
        {
            if (userId == 0 || !badges.Any())
                return;

            if (!await SignedInAsync())
            {
                App.Logger.Info("No session to ask with, leaving badge progress unknown");
                return;
            }

            try
            {
                foreach (var chunk in Chunk(badges.Select(x => x.Id)))
                {
                    var response = await Http.AuthGetJson<ApiArrayResponse<BadgeAwardedDate>>(
                        UrlBuilder.BuildApiUrl("badges", $"v1/users/{userId}/badges/awarded-dates?badgeIds={String.Join(',', chunk)}"));

                    if (response?.Data is null)
                        continue;

                    foreach (BadgeAwardedDate awarded in response.Data)
                    {
                        Badge? badge = badges.FirstOrDefault(x => x.Id == awarded.BadgeId);

                        if (badge is null)
                            continue;

                        badge.Awarded = true;
                        badge.AwardedDate = awarded.AwardedDate;
                    }
                }

                foreach (Badge badge in badges)
                    badge.AwardedKnown = true;
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to fetch awarded dates");
                App.Logger.Error(ex);
            }
        }

        private static async Task<bool> SignedInAsync()
        {
            if (!App.Settings.Prop.AllowCookieAccess)
                return false;

            if (!App.Cookies.Loaded)
                await Task.Run(App.Cookies.LoadCookies);

            return App.Cookies.Loaded;
        }

        private static async Task PopulateIconsAsync(List<Badge> badges)
        {
            if (!badges.Any())
                return;

            try
            {
                foreach (var chunk in Chunk(badges.Select(x => x.Id)))
                {
                    var response = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(
                        UrlBuilder.BuildApiUrl("thumbnails", $"v1/badges/icons?badgeIds={String.Join(',', chunk)}&size=150x150&format=Png&isCircular=false"));

                    if (response?.Data is null)
                        continue;

                    foreach (ThumbnailResponse thumbnail in response.Data)
                    {
                        Badge? badge = badges.FirstOrDefault(x => x.Id == thumbnail.TargetId);

                        if (badge is not null)
                            badge.IconUrl = thumbnail.ImageUrl;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to fetch badge icons");
                App.Logger.Error(ex);
            }
        }

        private static IEnumerable<List<long>> Chunk(IEnumerable<long> ids)
        {
            var batch = new List<long>(PageSize);

            foreach (long id in ids)
            {
                batch.Add(id);

                if (batch.Count < PageSize)
                    continue;

                yield return batch;

                batch = new List<long>(PageSize);
            }

            if (batch.Any())
                yield return batch;
        }
    }
}