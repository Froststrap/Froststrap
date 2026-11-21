// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Model;

/// <summary>
/// The operating system Froststrap is running on. Computed once, never inferred from a client.
/// </summary>
internal enum HostPlatform
{
    Windows,
    MacOS,
    Linux
}

/// <summary>
/// The product being run. Explicit, never inferred from an executable name or the host OS.
/// </summary>
internal enum ClientTarget
{
    Player,
    Studio
}

/// <summary>
/// How a client is packaged. Describes the client, not the host: a bundle layout must be
/// constructible on Linux.
/// </summary>
internal enum ExecutableShape
{
    FlatExecutable,
    MacBundle,
    ExternalManaged
}

/// <summary>
/// How a client emits logs. Determines which files the watcher can attach to.
/// </summary>
internal enum LogShape
{
    Native,
    PlainLog,
    External
}

internal enum CookieFormat
{
    None,
    ProtectedNetscape,
    BinaryCookies,
    PlainHeader
}

internal enum HostIntegration
{
    None,
    WindowsRegistry
}

internal enum ChannelStore
{
    None,
    Registry,
    MacDefaults
}

internal enum ModSupport
{
    None,
    OverlayOnly,
    BundleFiles
}

internal enum RestoreStrategy
{
    None,
    VersionBackup,
    PackageReextract
}

internal enum InstallReason
{
    /// <summary>The client is present and nothing needs to happen.</summary>
    None,
    FirstInstall,
    Missing,
    Forced,
    UpdateAvailable,
    Repair
}

internal enum InstallPhase
{
    Preparing,
    Downloading,
    Extracting,
    Verifying,
    Finalizing
}
