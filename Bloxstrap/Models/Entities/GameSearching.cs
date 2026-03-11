/*
 *  Froststrap
 *  Copyright (c) Froststrap Team
 *
 *  This file is part of Froststrap and is distributed under the terms of the
 *  GNU Affero General Public License, version 3 or later.
 *
 *  SPDX-License-Identifier: AGPL-3.0-or-later
 *
 *  Description: Nix flake for shipping for Nix-darwin, Nix, NixOS, and modules
 *               of the Nix ecosystem. 
 */
﻿using Bloxstrap.RobloxInterfaces;

namespace Bloxstrap.Models.Entities
{
    public static class GameSearching
    {
        private const string LOG_IDENT = "GameSearching";

        public static async Task<List<OmniSearchContent>> GetGameSearchResultsAsync(string searchQuery)
        {
            var results = new List<OmniSearchContent>();

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                App.Logger.WriteLine(LOG_IDENT, "Search query is empty.");
                return results;
            }

            try
            {
                string url = $"https://apis.{Deployment.RobloxDomain}/search-api/omni-search?searchQuery={Uri.EscapeDataString(searchQuery)}&sessionid=0&pageType=Game";

                var response = await Http.GetJson<OmniSearchResponse>(url);

                if (response?.SearchResults is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Search API returned no results.");
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
                App.Logger.WriteLine(LOG_IDENT, $"Error fetching search results: {ex.Message}");
            }

            return results;
        }
    }
}