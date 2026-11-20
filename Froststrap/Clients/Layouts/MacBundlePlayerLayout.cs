// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Layouts
{
    internal sealed class MacBundlePlayerLayout : ClientLayoutBase
    {
        public override string ClientId => "roblox-mac";

        public override ClientTarget Target => ClientTarget.Player;

        public override ExecutableShape Shape => ExecutableShape.MacBundle;

        public override string? BinaryType => "MacPlayer";

        public override string? ExecutableName => "RobloxPlayer.app";

        public override IReadOnlyList<string> ProcessNames => ["RobloxPlayer"];

        public override LogShape LogFormat => LogShape.Native;

        public override string? LogKindToken => "_Player_";

        public override ClientInstall Resolve(ClientResolveContext context)
        {
            ClientInstall install = VersionedInstall(context, isBundle: true);
            string bundle = Path.Combine(install.InstallRoot, ExecutableName!);
            string support = Path.Combine(context.UserProfile, "Library", "Application Support", "Roblox");
            string userData = Path.Combine(context.UserProfile, "Library", "Roblox");

            return install with
            {
                ExecutablePath = bundle,
                ContentRoot = Path.Combine(bundle, "Contents", "Resources"),
                ClientSettingsRoot = Path.Combine(bundle, "Contents", "MacOS"),
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
}
