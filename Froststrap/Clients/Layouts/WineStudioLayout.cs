// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Layouts;

/// <summary>
/// Windows Studio executed through Wine. Packaging is the Windows flat layout, but log,
/// cache, and cookie locations live inside a Wine prefix owned by the launcher, so they
/// are not resolved here yet.
/// </summary>
internal sealed class WineStudioLayout : ClientLayoutBase
{
    public override string ClientId => "wine-studio";

    public override ClientTarget Target => ClientTarget.Studio;

    public override ExecutableShape Shape => ExecutableShape.FlatExecutable;

    public override string? BinaryType => "WindowsStudio64";

    public override string? ExecutableName => "RobloxStudioBeta.exe";

    // Wine may report either the executable basename or the bundle-style name.
    public override IReadOnlyList<string> ProcessNames => ["RobloxStudioBeta", "RobloxStudio"];

    public override bool MatchProcessNamePrefix => true;

    public override LogShape LogFormat => LogShape.Native;

    public override string? LogKindToken => "_Studio_";

    public override ClientInstall Resolve(ClientResolveContext context)
    {
        ClientInstall install = VersionedInstall(context);

        return install with
        {
            ExecutablePath = Path.Combine(install.InstallRoot, ExecutableName!),
            // TODO(client-runtime): resolve these from the Wine prefix once the Studio
            // launcher owns the prefix. They are deliberately null rather than reusing
            // Sober's paths.
            SupportRoot = null,
            UserDataRoot = null,
            PluginsDirectory = null,
            LogsRoot = Path.Combine(context.DataRoot, "Wine", "prefixes", "studio", "drive_c", "users", context.UserName, "AppData", "Local", "Roblox", "logs"),
            CacheRoot = null,
            CookiesDirectory = null,
            ConfigDirectory = null,
            GlobalBasicSettingsPath = null
        };
    }
}
