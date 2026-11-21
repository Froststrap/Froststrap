// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;
using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;
using Froststrap.Models.Manifest;
using Froststrap.RobloxInterfaces;

namespace Froststrap.Clients.Sources;

internal sealed class DeploymentCdnSource(IClientLayout layout)
{
    private const string MacPlayerArchive = "RobloxPlayer.zip";
    private const string MacStudioArchive = "RobloxStudioApp.zip";

    private bool IsBundle => layout.Shape == ExecutableShape.MacBundle;

    public bool HasPackageSignatures => !IsBundle;

    public string DeployHistoryUrl => IsBundle
        ? "https://setup-rbxcdn.github.io/mac/DeployHistory.txt"
        : "https://setup-rbxcdn.github.io/DeployHistory.txt";

    public string DeployHistoryBinaryType => layout.Target == ClientTarget.Studio
        ? "Studio"
        : IsBundle ? "Client" : "WindowsPlayer";

    public async Task<Dictionary<string, string>> GetPackageMapAsync()
    {
        if (IsBundle)
        {
            return new Dictionary<string, string>
            {
                { MacPlayerArchive, "" },
                { MacStudioArchive, "" }
            };
        }

        await App.RemoteData.WaitUntilDataFetched();

        var targetMap = App.RemoteData.Prop.PackageMaps[layout.Target == ClientTarget.Studio ? "studio" : "player"];
        var map = new Dictionary<string, string>(App.RemoteData.Prop.PackageMaps.CommonPackageMap);

        foreach (var package in targetMap)
            map[package.Key] = package.Value;

        if (Path.DirectorySeparatorChar != '\\')
        {
            foreach (var key in map.Keys.ToList())
            {
                if (map[key] != null)
                    map[key] = map[key].Replace('\\', '/');
            }
        }

        return map;
    }

    public async Task<PackageManifest> GetPackageManifestAsync(string versionGuid)
    {
        if (IsBundle)
        {
            string archive = layout.Target == ClientTarget.Studio ? MacStudioArchive : MacPlayerArchive;
            return new PackageManifest($"v0\n{archive}\n{versionGuid}\n0\n0");
        }

        string manifestUrl = Deployment.GetLocation($"/{versionGuid}-rbxPkgManifest.txt");
        return new PackageManifest(await App.HttpClient.GetStringAsync(new Uri(manifestUrl)));
    }

    public string GetPackageUrl(string versionGuid, string packageName) => IsBundle
        ? Deployment.GetLocation($"{MacArchitecturePath}/{versionGuid}-{packageName}")
        : Deployment.GetLocation($"/{versionGuid}-{packageName}");

    private static string MacArchitecturePath =>
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "/mac/arm64" : "/mac";
}
