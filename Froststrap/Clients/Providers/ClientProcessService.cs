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
internal class ClientProcessService : IProcessService
{
    private readonly HashSet<string> _names;
    private readonly bool _prefix;

    private readonly string? _crashHandlerName;

    public ClientProcessService(IClientLayout layout)
    {
        ProcessNames = layout.ProcessNames;
        _crashHandlerName = layout.CrashHandlerProcessName;
        _prefix = layout.MatchProcessNamePrefix;
        CanFollowRelaunch = layout.Shape != ExecutableShape.FlatExecutable;
        _names = new HashSet<string>(
            layout.ProcessNames.Select(name => Path.GetFileNameWithoutExtension(name)),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> ProcessNames { get; }

    public bool CanFollowRelaunch { get; }

    public bool IsRunning() => FindAllPlayer().Count > 0;

    private Process[] FindCrashHandlers() => _crashHandlerName is null ? [] : Process.GetProcessesByName(_crashHandlerName);

    public IReadOnlyList<Process> FindAllPlayer()
        => Utility.Processes.GetProcessList().Where(Owns).ToList();

    public bool IsStudioProcess(Process process) => Owns(process);

    public int FindNewestStudioPid(IEnumerable<Process> processes)
    {
        int newestPid = 0;
        DateTime newestStartTime = DateTime.MinValue;

        foreach (Process process in processes)
        {
            if (!Owns(process))
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

    public virtual void Kill(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);

            App.Logger.Info($"Killing process '{process.ProcessName}' (pid={pid})");

            if (process.HasExited)
            {
                App.Logger.Info($"PID {pid} has already exited");
                return;
            }

            process.Kill();
        }
        catch (Exception ex)
        {
            App.Logger.Error($"PID {pid} could not be closed");
            App.Logger.Error(ex);
        }
    }

    public async Task CloseAsync(int pid, TimeSpan gracePeriod, CancellationToken cancellationToken = default)
    {
        using var process = Process.GetProcessById(pid);

        if (!process.CloseMainWindow())
            App.Logger.Warn($"Roblox (PID {pid}) has no main window to close");

        using (var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            wait.CancelAfter(gracePeriod);

            try
            {
                await process.WaitForExitAsync(wait.Token);
                return;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        // a join can land right as the wait times out
        cancellationToken.ThrowIfCancellationRequested();

        App.Logger.Warn($"Roblox (PID {pid}) didn't close, ending it");

        Kill(pid);

        // Kill only starts termination; stay "closing" until it's gone
        if (!await Task.Run(() => process.WaitForExit(TimeSpan.FromSeconds(3))))
            App.Logger.Warn($"Roblox (PID {pid}) still hasn't exited");
    }

    public async Task CloseCrashHandlersAsync(CancellationToken cancellationToken = default)
    {
        foreach (var crashProc in FindCrashHandlers())
        {
            try
            {
                App.Logger.Info($"Found crash handler: {crashProc.ProcessName} (PID {crashProc.Id})");

                crashProc.Kill();
                using var cts = new CancellationTokenSource();
                var delay = Task.Delay(3000, cts.Token);
                var wait = crashProc.WaitForExitAsync(cancellationToken);
                await Task.WhenAny(wait, delay);
                await cts.CancelAsync();

                App.Logger.Info(
                    crashProc.HasExited
                        ? $"Crash handler {crashProc.Id} exited successfully"
                        : $"Crash handler {crashProc.Id} is still running"
                );
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to close crash handler {crashProc.Id}: {ex}");
            }
        }
    }

    public async Task CloseAllAsync(KillRequest request, CancellationToken cancellationToken = default)
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

                if (request.WaitForExit)
                    await process.WaitForExitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Process {process.Id} could not be closed: {ex.Message}");
            }
        }

    }

    public bool Owns(Process process)
    {
        try
        {
            string name = Path.GetFileNameWithoutExtension(process.ProcessName);

            if (_names.Contains(name))
                return true;

            return _prefix && _names.Any(expected => name.StartsWith(expected, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
