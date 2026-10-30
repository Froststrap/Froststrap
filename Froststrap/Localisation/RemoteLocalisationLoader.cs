// SPDX-FileCopyrightText: 2026 Froststrap
// SPDX-License-Identifier: MPL-2.0

using System.Net.Http;

namespace Froststrap.Localisation;

public sealed class RemoteLocalisationLoader
{
    private static readonly HttpClient s_client = new();

    public static async Task LoadAsync(string cultureName, string url, CancellationToken ct = default)
    {
        try
        {
            using var response = await s_client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(text))
                Localizer.Instance.LoadCultureFromString(cultureName, text);
        }
        catch { /* silent: keep existing local translations */ }
    }
}
