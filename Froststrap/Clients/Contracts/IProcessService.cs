// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Contracts
{
    /// <summary>
    /// Which OS processes belong to a client, and how to close them.
    /// </summary>
    internal interface IProcessService
    {
        IReadOnlyList<string> ProcessNames { get; }

        bool IsRunning();

        bool Owns(Process process);

        Process? FindPlayer();

        IReadOnlyList<Process> FindAllPlayer();

        Process[] FindCrashHandlers();

        bool IsStudioProcess(Process process);

        int FindNewestStudioPid(IEnumerable<Process> processes);

        Task KillAllAsync(KillRequest request, CancellationToken cancellationToken = default);
    }
}
