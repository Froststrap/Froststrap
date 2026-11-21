// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Contracts;

/// <summary>
/// Which OS processes belong to a client, and how to close them.
/// </summary>
internal interface IProcessService
{
    IReadOnlyList<string> ProcessNames { get; }

    bool CanFollowRelaunch { get; }

    bool IsRunning();

    bool Owns(Process process);

    IReadOnlyList<Process> FindAllPlayer();

    bool IsStudioProcess(Process process);

    int FindNewestStudioPid(IEnumerable<Process> processes);

    void Kill(int pid);

    Task CloseAsync(int pid, TimeSpan gracePeriod, CancellationToken cancellationToken = default);

    Task CloseAllAsync(KillRequest request, CancellationToken cancellationToken = default);

    Task CloseCrashHandlersAsync(CancellationToken cancellationToken = default);
}
