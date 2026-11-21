// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Integrations;

internal static class BetterMatchmaking
{
    public static async Task<string> FindServerAsync(long placeId, Action<string> setStatus, CancellationToken cancellationToken = default)
    {
        string selectedRegion = App.Settings.Prop.SelectedRegion ?? "";

        using var fetcher = new RobloxServerFetcher();

        if (!string.IsNullOrEmpty(selectedRegion) &&
            !selectedRegion.Equals(Strings.Common_Auto, StringComparison.OrdinalIgnoreCase))
        {
            App.Logger.Debug($"User selected specific region: {selectedRegion}");

            setStatus(string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_SearchingServers, selectedRegion));

            var result = await fetcher.FindBestServerInSelectedRegionAsync(
                placeId,
                selectedRegion,
                cancellationToken: cancellationToken);

            if (result.Found)
            {
                App.Logger.Info($"Found server in selected region {selectedRegion}: {result.ServerId}");
                return result.ServerId!;
            }

            App.Logger.Info($"No servers found in selected region {selectedRegion}. Falling back to Auto mode.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            App.Logger.Debug("Matchmaking was cancelled before auto mode could start.");
            return "";
        }

        cancellationToken.ThrowIfCancellationRequested();

        setStatus(Strings.Bootstrapper_Status_SearchingNearbyServers);

        var topRegions = await fetcher.GetClosestRegionsForAutoModeAsync(cancellationToken);

        if (cancellationToken.IsCancellationRequested)
            return "";

        if (topRegions.Count == 0)
            throw new HttpRequestException("No regions found from datacenter list");

        var autoResult = await fetcher.FindBestServerInRegionAsync(
            placeId,
            topRegions,
            cancellationToken: cancellationToken);

        if (autoResult.Found)
        {
            App.Logger.Info($"Selected best server in {autoResult.Region} (rank {autoResult.Rank})");
            return autoResult.ServerId!;
        }

        App.Logger.Warn("No server found in any region within timeout.");
        return "";
    }
}
