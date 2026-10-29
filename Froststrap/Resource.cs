// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Platform;

namespace Froststrap
{
    static class Resource
    {
        static readonly Uri[] assetUris = AssetLoader.GetAssets(new Uri("avares://Froststrap/"), null).ToArray();

        public static Stream GetStream(string name)
        {
            if (name.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            {
                return AssetLoader.Open(new Uri(name));
            }

            string normalizedName = name.Replace('\\', '/').TrimStart('/');

            var targetUri = assetUris.FirstOrDefault(uri =>
                uri.AbsolutePath.EndsWith(normalizedName, StringComparison.OrdinalIgnoreCase) ||
                uri.OriginalString.EndsWith(normalizedName, StringComparison.OrdinalIgnoreCase));

            if (targetUri == null && normalizedName.Contains('.', StringComparison.Ordinal))
            {
                string pathFromDots = normalizedName.Replace('.', '/');
                int lastSlash = pathFromDots.LastIndexOf('/');
                if (lastSlash > 0)
                {
                    pathFromDots = string.Concat(pathFromDots.AsSpan(0, lastSlash), ".", pathFromDots.AsSpan(lastSlash + 1));
                }

                targetUri = assetUris.FirstOrDefault(uri =>
                    uri.AbsolutePath.EndsWith(pathFromDots, StringComparison.OrdinalIgnoreCase));
            }

            if (targetUri == null)
            {
                throw new FileNotFoundException(
                    $"Avalonia resource matching '{name}' (normalized: '{normalizedName}') was not found. " +
                    $"Available assets count: {assetUris.Length}. Sample assets: " +
                    string.Join(", ", assetUris.Take(5).Select(u => u.AbsolutePath)));
            }

            return AssetLoader.Open(targetUri);
        }

        public static async Task<byte[]> Get(string name)
        {
            using var stream = GetStream(name);
            using var memoryStream = new MemoryStream();

            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }

        public static async Task<string> GetString(string name)
        {
            return Encoding.UTF8.GetString(await Get(name));
        }
    }
}
