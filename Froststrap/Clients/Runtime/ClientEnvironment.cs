// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;
using Froststrap.Models.Persistable;

namespace Froststrap.Clients.Runtime;

/// <summary>
/// Host values captured once at startup. Layouts stay pure: they receive these through
/// <see cref="ClientResolveContext"/> instead of reading <c>Paths</c> or <c>App.Settings</c>.
/// </summary>
internal sealed class ClientEnvironment
{
    private readonly Func<bool> _staticDirectory;

    public ClientEnvironment(
        HostPlatform host,
        string versionsRoot,
        string userProfile,
        string localAppData,
        string tempRoot,
        Func<bool> staticDirectory,
        string dataRoot = "",
        string userName = "")
    {
        DataRoot = dataRoot;
        UserName = userName;
        Host = host;
        VersionsRoot = versionsRoot;
        UserProfile = userProfile;
        LocalAppData = localAppData;
        TempRoot = tempRoot;
        _staticDirectory = staticDirectory;
    }

    public HostPlatform Host { get; }

    public string VersionsRoot { get; }

    public string UserProfile { get; }

    public string LocalAppData { get; }

    public string TempRoot { get; }

    public string DataRoot { get; }

    public string UserName { get; }

    public static HostPlatform DetectHost()
    {
        if (OperatingSystem.IsWindows())
            return HostPlatform.Windows;

        if (OperatingSystem.IsMacOS())
            return HostPlatform.MacOS;

        return HostPlatform.Linux;
    }

    public static ClientEnvironment FromHost() => new(
        DetectHost(),
        Paths.Versions,
        Paths.UserProfile,
        Paths.LocalAppData,
        Path.GetTempPath(),
        () => App.Settings.Prop.StaticDirectory,
        Paths.Base,
        Environment.UserName);

    /// <summary>
    /// Builds the host-independent context a layout needs. The static-directory setting is
    /// read live so toggling it keeps working without rebuilding the registry.
    /// </summary>
    public ClientResolveContext CreateContext(DistributionState state) => new()
    {
        VersionsRoot = VersionsRoot,
        UserProfile = UserProfile,
        LocalAppData = LocalAppData,
        TempRoot = TempRoot,
        DataRoot = DataRoot,
        UserName = UserName,
        StaticDirectory = _staticDirectory(),
        State = state
    };
}
