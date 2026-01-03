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

namespace Froststrap.Models.Entities
{
    public static class GameSearching
    {
        const string LOG_IDENT = "GameSearching";
        private static readonly HttpClient _client = new HttpClient();

        public static async Task<List<GameSearchResult>> GetGameSearchResultsAsync(string searchQuery)
        {
            const string SearchApiUrl = "https://apis.roblox.com/search-api/omni-search";
            var results = new List<GameSearchResult>();

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                App.Logger.WriteLine(LOG_IDENT, "Search query is empty.");
                return results;
            }

            try
            {
                string requestUrl = $"{SearchApiUrl}?searchQuery={Uri.EscapeDataString(searchQuery)}&sessionid=0&pageType=Game";

                var response = await _client.GetAsync(requestUrl).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to fetch search results. Status code: {response.StatusCode}");
                    return results;
                }

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var jsonDoc = JsonDocument.Parse(content);

                if (!jsonDoc.RootElement.TryGetProperty("searchResults", out var searchResultsElem) || searchResultsElem.ValueKind != JsonValueKind.Array)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Search API returned no searchResults array.");
                    return results;
                }

                var seen = new HashSet<long>();
                int taken = 0;

                foreach (var group in searchResultsElem.EnumerateArray())
                {
                    if (taken >= 5) break;
                    if (group.ValueKind != JsonValueKind.Object) continue;
                    if (!group.TryGetProperty("contents", out var contentsElem) || contentsElem.ValueKind != JsonValueKind.Array) continue;

                    foreach (var contentItem in contentsElem.EnumerateArray())
                    {
                        if (taken >= 5) break;
                        if (contentItem.ValueKind != JsonValueKind.Object) continue;

                        if (!contentItem.TryGetProperty("rootPlaceId", out var rootPlaceElem))
                            continue;

                        long rootId = 0;
                        if (rootPlaceElem.ValueKind == JsonValueKind.Number && rootPlaceElem.TryGetInt64(out var num))
                            rootId = num;
                        else if (rootPlaceElem.ValueKind == JsonValueKind.String && long.TryParse(rootPlaceElem.GetString(), out var parsed))
                            rootId = parsed;
                        else
                            continue;

                        if (rootId == 0 || !seen.Add(rootId))
                            continue;

                        string name = "";
                        if (contentItem.TryGetProperty("name", out var nameElem) && nameElem.ValueKind == JsonValueKind.String)
                            name = nameElem.GetString() ?? "";

                        int? players = null;
                        if (contentItem.TryGetProperty("playerCount", out var pcElem) && pcElem.ValueKind == JsonValueKind.Number && pcElem.TryGetInt32(out var pc))
                            players = pc;

                        results.Add(new GameSearchResult
                        {
                            RootPlaceId = rootId,
                            Name = string.IsNullOrWhiteSpace(name) ? $"Place {rootId}" : name,
                            PlayerCount = players
                        });

                        taken++;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Error fetching search results: {ex.Message}");
            }

            return results;
        }

        public static async Task<List<long>> GetPlaceIdsFromSearchQueryAsync(string searchQuery)
        {
            var results = await GetGameSearchResultsAsync(searchQuery).ConfigureAwait(false);
            return results.Select(r => r.RootPlaceId).ToList();
        }
    }
}