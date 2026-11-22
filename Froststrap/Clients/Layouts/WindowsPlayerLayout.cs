// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Layouts;

internal sealed class WindowsPlayerLayout : ClientLayoutBase
{
    public override string ClientId => "roblox-win";

    public override ClientTarget Target => ClientTarget.Player;

    public override ExecutableShape Shape => ExecutableShape.FlatExecutable;

    public override string? BinaryType => "WindowsPlayer";

    public override string? ExecutableName => "RobloxPlayerBeta.exe";

    public override IReadOnlyList<string> ProcessNames => ["RobloxPlayerBeta"];

    public override CookieFormat CookieFormat => CookieFormat.ProtectedNetscape;

    public override ChannelStore ChannelStore => ChannelStore.Registry;

    public override HostIntegration HostIntegration => HostIntegration.WindowsRegistry;

    public override LogShape LogFormat => LogShape.Native;

    public override string? LogKindToken => "_Player_";

    public override IReadOnlyList<string> RequiredFiles => ["WebView2Loader.dll", "RobloxPlayerBeta.dll"];

    public override ClientInstall Resolve(ClientResolveContext context)
    {
        ClientInstall install = VersionedInstall(context);
        string support = Path.Combine(context.LocalAppData, "Roblox");

        return install with
        {
            ExecutablePath = Path.Combine(install.InstallRoot, ExecutableName!),
            SupportRoot = support,
            UserDataRoot = support,
            PluginsDirectory = Path.Combine(support, "Plugins"),
            LogsRoot = Path.Combine(support, "logs"),
            CacheRoot = Path.Combine(context.TempRoot, "Roblox"),
            CookiesDirectory = Path.Combine(support, "LocalStorage"),
            AppStoragePath = Path.Combine(support, "LocalStorage", "appStorage.json"),
            GlobalBasicSettingsPath = Path.Combine(support, "GlobalBasicSettings_13.xml")
        };
    }
}
