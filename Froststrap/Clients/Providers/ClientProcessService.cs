// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;

namespace Froststrap.Clients.Providers;

/// <summary>
/// Which OS processes belong to this client. Names come from the layout, so no caller
/// needs to know a process-name literal.
/// </summary>
/// <remarks>
/// Obtained from a resolved client, so "player" methods mean "this client's processes".
/// A Studio client's <see cref="FindAllPlayer"/> therefore returns Studio processes; no
/// caller should ask a Studio client for a player process.
/// </remarks>
internal sealed class ClientProcessService : IProcessService
{
    private readonly HashSet<string> _names;

    public ClientProcessService(IClientLayout layout)
    {
        ProcessNames = layout.ProcessNames;
        _names = new HashSet<string>(
            layout.ProcessNames.Select(Path.GetFileNameWithoutExtension),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> ProcessNames { get; }

    public bool IsRunning() => FindAllPlayer().Count > 0;

    public Process? FindPlayer() => FindAllPlayer().FirstOrDefault();

    public IReadOnlyList<Process> FindAllPlayer()
        => Utility.Processes.GetProcessesSafe().Where(Matches).ToList();

    public bool IsStudioProcess(Process process) => Matches(process);

    public int FindNewestStudioPid(IEnumerable<Process> processes)
    {
        int newestPid = 0;
        DateTime newestStartTime = DateTime.MinValue;

        foreach (Process process in processes)
        {
            if (!Matches(process))
                continue;

            try
            {
                if (process.StartTime > newestStartTime)
                {
                    newestStartTime = process.StartTime;
                    newestPid = process.Id;
                }
            }
            catch (Exception)
            {
                // Process exited or is not readable; skip it.
            }
        }

        return newestPid;
    }

    public Task KillAllAsync(KillRequest request, CancellationToken cancellationToken = default)
    {
        foreach (Process process in FindAllPlayer())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (process.HasExited)
                    continue;

                if (request.MinimumAge > TimeSpan.Zero && DateTime.Now - process.StartTime < request.MinimumAge)
                {
                    App.Logger.Info("Skipping new process");
                    continue;
                }

                App.Logger.Info($"Closing '{process.ProcessName}' (pid={process.Id}, force={request.Force})");

                if (request.Force)
                    process.Kill();
                else
                    process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Process {process.Id} could not be closed: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    private bool Matches(Process process)
    {
        try
        {
            return _names.Contains(Path.GetFileNameWithoutExtension(process.ProcessName));
        }
        catch
        {
            return false;
        }
    }
}
