// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

// Might be smart to make this its our service elsewhere so other classes can use it

namespace Froststrap.Integrations.AccountManager
{
    internal class AvatarService
    {
        private readonly Lock _cacheLock = new();
        private readonly Dictionary<long, string?> _avatarUrlCache = [];

        public async Task<Dictionary<long, string?>> GetAvatarUrlsBulkAsync(List<long> userIds)
        {
            var result = new Dictionary<long, string?>();
            if (userIds == null || userIds.Count == 0) return result;

            var missing = new List<long>();

            lock (_cacheLock)
            {
                foreach (long userId in userIds.Distinct())
                {
                    if (_avatarUrlCache.TryGetValue(userId, out string? cachedUrl))
                        result[userId] = cachedUrl;
                    else
                        missing.Add(userId);
                }
            }

            if (missing.Count == 0)
                return result;

            const int batchSize = 100;

            for (int i = 0; i < missing.Count; i += batchSize)
            {
                var batch = missing.Skip(i).Take(batchSize).ToList();
                string idsParam = string.Join(',', batch);
                var uriBuilder = new UriBuilder(UrlBuilder.BuildApiUrl("thumbnails", "v1/users/avatar-headshot", secure: true))
                {
                    Query = $"userIds={idsParam}&size=75x75&format=Png&isCircular=true"
                };

                try
                {
                    var response = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(uriBuilder.Uri);

                    if (response?.Data != null)
                    {
                        lock (_cacheLock)
                        {
                            foreach (var item in response.Data)
                            {
                                if (item.TargetId > 0 && !string.IsNullOrEmpty(item.ImageUrl))
                                {
                                    result[item.TargetId] = item.ImageUrl;
                                    _avatarUrlCache[item.TargetId] = item.ImageUrl;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Batch failed: {ex.Message}");
                }
            }

            return result;
        }

        public string? GetCachedAvatarUrl(long userId)
        {
            lock (_cacheLock)
                return _avatarUrlCache.TryGetValue(userId, out var url) ? url : null;
        }
    }
}
