// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Layouts;

internal sealed class MacBundleStudioLayout : ClientLayoutBase
{
    public override string ClientId => "roblox-mac";

    public override ClientTarget Target => ClientTarget.Studio;

    public override ExecutableShape Shape => ExecutableShape.MacBundle;

    public override string? BinaryType => "MacStudio";

    public override string? ExecutableName => "RobloxStudio.app";

    public override string? SigningTeamId => "2CFABCH843";

    public override IReadOnlyList<string> ProcessNames => ["RobloxStudio"];

    public override CookieFormat CookieFormat => CookieFormat.BinaryCookies;

    public override ChannelStore ChannelStore => ChannelStore.MacDefaults;

    public override LogShape LogFormat => LogShape.Native;

    public override string? LogKindToken => "_Studio_";

    public override (string ContentRoot, string ClientSettingsRoot) ModRoots(string versionRoot)
    {
        string contents = Path.Combine(versionRoot, ExecutableName!, "Contents");
        return (Path.Combine(contents, "Resources"), Path.Combine(contents, "MacOS"));
    }

    public override ClientInstall Resolve(ClientResolveContext context)
    {
        ClientInstall install = VersionedInstall(context, isBundle: true);
        string bundle = Path.Combine(install.InstallRoot, ExecutableName!);
        var (contentRoot, clientSettingsRoot) = ModRoots(install.InstallRoot);
        string support = Path.Combine(context.UserProfile, "Library", "Application Support", "Roblox");
        string userData = Path.Combine(context.UserProfile, "Library", "Roblox");

        return install with
        {
            ExecutablePath = bundle,
            ContentRoot = contentRoot,
            ClientSettingsRoot = clientSettingsRoot,
            SupportRoot = support,
            UserDataRoot = userData,
            PluginsDirectory = Path.Combine(support, "Plugins"),
            LogsRoot = Path.Combine(context.UserProfile, "Library", "Logs", "Roblox"),
            CacheRoot = Path.Combine(context.UserProfile, "Library", "Caches", "com.roblox.RobloxPlayer"),
            CookiesDirectory = Path.Combine(context.UserProfile, "Library", "HTTPStorages"),
            AppStoragePath = Path.Combine(support, "LocalStorage", "appStorage.json"),
            GlobalBasicSettingsPath = Path.Combine(userData, "GlobalBasicSettings_13.xml")
        };
    }
}
