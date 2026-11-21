// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Contracts;

/// <summary>
/// Where a client's logs live and which files count as game logs. Owns discovery only;
/// line parsing stays in the shared watcher.
/// </summary>
internal interface ILogLocator
{
    /// <summary>Null when the directory is owned by a launcher (for example a Wine prefix).</summary>
    string? LogsRoot { get; }

    LogShape LogFormat { get; }

    IReadOnlyList<string> ReadySignals { get; }

    bool IsGameLog(FileInfo file);

    Task<FileInfo?> FindLatestAsync(DateTime notBefore, CancellationToken cancellationToken = default);

    /// <summary>
    /// Follows rotation, such as a Studio place-open producing a new log file.
    /// </summary>
    FileInfo? FindNewer(FileInfo current);
}
