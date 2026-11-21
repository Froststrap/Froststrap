// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Layouts;

/// <summary>
/// Sober runs on Linux but does not share the native client's packaging or log shape:
/// it is flatpak-managed and writes plain <c>.log</c> files.
/// </summary>
internal sealed class SoberLayout : ClientLayoutBase
{
    public override string ClientId => "sober";

    public override ClientTarget Target => ClientTarget.Player;

    public override ExecutableShape Shape => ExecutableShape.ExternalManaged;

    public override string? BinaryType => null;

    public override string? ExecutableName => null;

    public override IReadOnlyList<string> ProcessNames => ["sober"];

    public override string? CrashHandlerProcessName => null;

    public override CookieFormat CookieFormat => CookieFormat.PlainHeader;

    public override LogShape LogFormat => LogShape.PlainLog;

    public override string? LogKindToken => null;

    public override IReadOnlyList<string> ReadySignals =>
    [
        "will_handle_app_startup",
        "will_handle_start_game"
    ];

    public override IReadOnlyList<string> LeaveSignals =>
    [
        "app_interface$json: {\"type\":\"game_left\"}",
        "app: lifecycle: will_do_clean_exit"
    ];

    public override IReadOnlyList<string> ExcludedLogTokens => [];

    public override ClientInstall Resolve(ClientResolveContext context)
    {
        string root = Path.Combine(context.VersionsRoot, "Sober");
        string data = Path.Combine(root, "data", "sober");

        return new ClientInstall
        {
            ClientId = ClientId,
            Target = Target,
            VersionGuid = context.State.VersionGuid,
            InstallRoot = root,
            // Roblox's game content for Sober lives in the asset overlay; this is what the
            // bootstrapper treats as the "version directory" and what mods are applied to.
            ContentRoot = Path.Combine(data, "asset_overlay"),
            ClientSettingsRoot = data,
            ExecutablePath = null,
            SupportRoot = data,
            UserDataRoot = data,
            ConfigDirectory = Path.Combine(root, "config", "sober"),
            LogsRoot = Path.Combine(data, "sober_logs"),
            CacheRoot = Path.Combine(root, "cache"),
            CookiesDirectory = Path.Combine(data, "cookies"),
            AppStoragePath = Path.Combine(data, "appData", "LocalStorage", "appStorage.json"),
            GlobalBasicSettingsPath = Path.Combine(data, "appData", "GlobalBasicSettings_13.xml"),
            IsExternal = true
        };
    }
}
