// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.RobloxInterfaces;

namespace Froststrap.Models.Entities
{
    internal static class GameSearching
    {
        public static async Task<List<OmniSearchContent>> GetGameSearchResultsAsync(string searchQuery)
        {
            var results = new List<OmniSearchContent>();

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                App.Logger.Error("Search query is empty.");
                return results;
            }

            try
            {
                Uri omniSearchUrl = new($"https://apis.{Deployment.RobloxDomain}/search-api/omni-search?searchQuery={Uri.EscapeDataString(searchQuery)}&sessionid=0&pageType=Game");

                var response = await Http.GetJson<OmniSearchResponse>(omniSearchUrl);

                if (response?.SearchResults is null)
                {
                    App.Logger.Error("Search API returned no results.");
                    return results;
                }

                var seenUniverses = new HashSet<ulong>();

                foreach (var group in response.SearchResults)
                {
                    if (results.Count >= 5) break;

                    if (group.Contents is null) continue;

                    foreach (var item in group.Contents)
                    {
                        if (results.Count >= 5) break;

                        if (item.UniverseId == 0 || !seenUniverses.Add(item.UniverseId))
                            continue;

                        results.Add(new OmniSearchContent
                        {
                            UniverseId = item.UniverseId,
                            RootPlaceId = item.RootPlaceId,
                            Name = item.Name ?? $"Game {item.UniverseId}",
                            PlayerCount = item.PlayerCount
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Error fetching search results: {ex.Message}");
            }

            return results;
        }
    }
}
