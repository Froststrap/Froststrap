// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Model;

/// <summary>
/// One resolved install for a version. Produced by <c>IClientLayout.Resolve</c>, which is
/// pure: whether the files actually exist is reported by <c>IRobloxClient.Inspect</c>.
/// </summary>
/// <remarks>
/// The path members are separated so no consumer has to guess what a directory means:
/// <list type="bullet">
/// <item><see cref="SupportRoot"/> is the client's support directory.</item>
/// <item><see cref="UserDataRoot"/> is the directory users open to see client files.</item>
/// <item><see cref="ContentRoot"/> is where game content and mods live.</item>
/// <item><see cref="ClientSettingsRoot"/> is where <c>ClientSettings</c> and FFlags live.</item>
/// </list>
/// Any of them may be null when a client does not expose the concept.
/// </remarks>
internal sealed record ClientInstall
{
    public required string ClientId { get; init; }

    public required ClientTarget Target { get; init; }

    public required string VersionGuid { get; init; }

    public required string InstallRoot { get; init; }

    public required string ContentRoot { get; init; }

    public required string ClientSettingsRoot { get; init; }

    /// <summary>
    /// File for a flat executable, directory for a macOS bundle, null for an externally
    /// managed client.
    /// </summary>
    public string? ExecutablePath { get; init; }

    public string? LogsRoot { get; init; }

    public string? CacheRoot { get; init; }

    /// <summary>Directory that holds the cookie store, not the file itself.</summary>
    public string? CookiesDirectory { get; init; }

    public string? SupportRoot { get; init; }

    public string? UserDataRoot { get; init; }

    public string? PluginsDirectory { get; init; }

    /// <summary>Launcher/tool configuration directory, when the client has one.</summary>
    public string? ConfigDirectory { get; init; }

    public string? AppStoragePath { get; init; }

    public string? GlobalBasicSettingsPath { get; init; }

    public bool IsBundle { get; init; }

    public bool IsExternal { get; init; }
}
