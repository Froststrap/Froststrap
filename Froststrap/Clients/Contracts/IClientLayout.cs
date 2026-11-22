// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Contracts;

/// <summary>
/// Static description of a client's packaging, paths, logs, and process names.
/// Immutable, state-free, and host-independent.
/// </summary>
internal interface IClientLayout
{
    string ClientId { get; }

    ClientTarget Target { get; }

    /// <summary>Packaging shape, not the host OS.</summary>
    ExecutableShape Shape { get; }

    /// <summary>Deployment binary type, or null for an externally managed client.</summary>
    string? BinaryType { get; }

    string? ExecutableName { get; }

    IReadOnlyList<string> ProcessNames { get; }

    /// <summary>
    /// True when the host may truncate process names, so a name only has to start with one
    /// of <see cref="ProcessNames"/>.
    /// </summary>
    bool MatchProcessNamePrefix { get; }

    string? CrashHandlerProcessName { get; }

    string? SigningTeamId { get; }

    CookieFormat CookieFormat { get; }

    ChannelStore ChannelStore { get; }

    HostIntegration HostIntegration { get; }

    LogShape LogFormat { get; }

    /// <summary><c>"_Player_"</c>, <c>"_Studio_"</c>, or null for a plain-log client.</summary>
    string? LogKindToken { get; }

    /// <summary>Log lines that mean the client window is ready.</summary>
    IReadOnlyList<string> ReadySignals { get; }

    IReadOnlyList<string> LeaveSignals { get; }

    /// <summary>Filename tokens that are never game logs.</summary>
    IReadOnlyList<string> ExcludedLogTokens { get; }

    /// <summary>Files that must exist in the install root for it to be usable.</summary>
    IReadOnlyList<string> RequiredFiles { get; }

    /// <summary>Where game content and client settings live inside a version directory.</summary>
    (string ContentRoot, string ClientSettingsRoot) ModRoots(string versionRoot);

    ClientInstall Resolve(ClientResolveContext context);
}
