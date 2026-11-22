// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;

namespace Froststrap.Clients.Providers;

/// <summary>
/// Launcher-managed clients such as Sober: every non-symlinked <c>.log</c> in the log
/// directory is a game log and there is no Studio rotation.
/// </summary>
internal sealed class PlainLogLocator(IClientLayout layout, Func<string?> logsRoot)
    : LogLocatorBase(layout, logsRoot)
{
    public override bool IsGameLog(FileInfo file)
        => file.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase) && file.LinkTarget is null;

    public override FileInfo? FindNewer(FileInfo current) => null;
}
