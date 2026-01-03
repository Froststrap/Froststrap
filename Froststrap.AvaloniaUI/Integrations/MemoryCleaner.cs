/*
 *  Froststrap
 *  Copyright (c) Froststrap Team
 *
 *  This file is part of Froststrap and is distributed under the terms of the
 *  GNU Affero General Public License, version 3 or later.
 *
 *  SPDX-License-Identifier: AGPL-3.0-or-later
 *
 *  Description: Nix flake for shipping for Nix-darwin, Nix, NixOS, and modules
 *               of the Nix ecosystem. 
 */

using System.Runtime.InteropServices;
using System.Timers;
using System.Diagnostics;
using System.ComponentModel;

namespace Froststrap.Integrations
{
    public class MemoryCleaner : IDisposable
    {
        private const string LOG_IDENT = "MemoryCleaner";

        private System.Timers.Timer? _cleanupTimer;
        private System.Timers.Timer? _robloxTrimTimer;
        private bool _isCleaning = false;
        private readonly object _cleanupLock = new object();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern int EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        private readonly HashSet<string> _criticalProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "system", "csrss", "wininit", "winlogon", "services", "lsass",
            "smss", "svchost", "explorer", "taskhostw", "dwm", "ctfmon"
        };

        private readonly HashSet<string> _robloxProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "robloxplayerbeta", "robloxstudiobeta" };

        public MemoryCleaner()
        {
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            const string LOG_IDENT_INIT = $"{LOG_IDENT}::InitializeTimer";

            try
            {
                UpdateTimer();
                UpdateRobloxTrimTimer();
                App.Logger.WriteLine(LOG_IDENT_INIT, "Memory cleaner initialized");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_INIT, $"Exception during initialization: {ex.Message}");
            }
        }

        private void UpdateTimer()
        {
            _cleanupTimer?.Stop();
            _cleanupTimer?.Dispose();
            _cleanupTimer = null;

            var interval = App.Settings.Prop.MemoryCleanerInterval;

            if (interval == MemoryCleanerInterval.Never)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::UpdateTimer", "Automatic cleaning disabled");
                return;
            }

            _cleanupTimer = new System.Timers.Timer();
            _cleanupTimer.Interval = GetIntervalMilliseconds(interval);
            _cleanupTimer.Elapsed += OnCleanupTimerElapsed;
            _cleanupTimer.AutoReset = true;
            _cleanupTimer.Start();

            App.Logger.WriteLine($"{LOG_IDENT}::UpdateTimer", $"Timer started with {interval} interval");
        }

        private void UpdateRobloxTrimTimer()
        {
            _robloxTrimTimer?.Stop();
            _robloxTrimTimer?.Dispose();
            _robloxTrimTimer = null;

            if (!App.Settings.Prop.EnableRobloxTrim || App.Settings.Prop.RobloxTrimIntervalSeconds <= 0)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::UpdateRobloxTrimTimer", "Roblox-specific trimming disabled");
                return;
            }

            int intervalSeconds = App.Settings.Prop.RobloxTrimIntervalSeconds;

            _robloxTrimTimer = new System.Timers.Timer();
            _robloxTrimTimer.Interval = intervalSeconds * 1000;
            _robloxTrimTimer.Elapsed += OnRobloxTrimTimerElapsed;
            _robloxTrimTimer.AutoReset = true;
            _robloxTrimTimer.Start();

            App.Logger.WriteLine($"{LOG_IDENT}::UpdateRobloxTrimTimer", $"Roblox trim timer started with {intervalSeconds} second interval");
        }

        private void OnRobloxTrimTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            const string LOG_IDENT_TIMER = $"{LOG_IDENT}::OnRobloxTrimTimerElapsed";

            try
            {
                if (_isCleaning)
                {
                    App.Logger.WriteLine(LOG_IDENT_TIMER, "Cleanup already in progress, skipping Roblox trim...");
                    return;
                }

                lock (_cleanupLock)
                {
                    _isCleaning = true;
                }

                try
                {
                    TrimRobloxProcesses();
                }
                finally
                {
                    lock (_cleanupLock)
                    {
                        _isCleaning = false;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_TIMER, $"Exception: {ex.Message}");
            }
        }

        private void OnCleanupTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            const string LOG_IDENT_TIMER = $"{LOG_IDENT}::OnCleanupTimerElapsed";

            try
            {
                if (_isCleaning)
                {
                    App.Logger.WriteLine(LOG_IDENT_TIMER, "Cleanup already in progress, skipping...");
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_TIMER, "Timer elapsed, performing optimized memory cleanup");
                CleanMemory();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_TIMER, $"Exception: {ex.Message}");
            }
        }

        public void CleanMemory()
        {
            const string LOG_IDENT_CLEAN = $"{LOG_IDENT}::CleanMemory";

            lock (_cleanupLock)
            {
                if (_isCleaning)
                    return;

                _isCleaning = true;
            }

            try
            {
                long beforeMemory = GetTotalMemoryUsage();
                App.Logger.WriteLine(LOG_IDENT_CLEAN, $"Total memory before cleanup: {FormatBytes(beforeMemory)}");

                CleanStandbyList();
                CleanProcessWorkingSets();

                long afterMemory = GetTotalMemoryUsage();
                long memoryFreed = beforeMemory - afterMemory;

                App.Logger.WriteLine(LOG_IDENT_CLEAN, $"Total memory after cleanup: {FormatBytes(afterMemory)}");
                App.Logger.WriteLine(LOG_IDENT_CLEAN, $"Freed {FormatBytes(memoryFreed)} of memory");

                OnMemoryCleaned?.Invoke(this, memoryFreed);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_CLEAN, $"Exception: {ex.Message}");
                OnCleanupError?.Invoke(this, ex.Message);
            }
            finally
            {
                lock (_cleanupLock)
                {
                    _isCleaning = false;
                }
            }
        }

        public void TrimRobloxProcesses()
        {
            const string LOG_IDENT_ROBLOX = $"{LOG_IDENT}::TrimRobloxProcesses";

            try
            {
                int robloxProcessesFound = 0;
                int robloxProcessesTrimmed = 0;
                long totalRobloxMemoryFreed = 0;

                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        string processName = process.ProcessName.ToLower();

                        if (_robloxProcessNames.Contains(processName) && !process.HasExited)
                        {
                            robloxProcessesFound++;

                            long beforeMemory = process.WorkingSet64;

                            if (TrimRobloxProcessMemory(process))
                            {
                                process.Refresh();
                                long afterMemory = process.WorkingSet64;
                                long memoryFreed = beforeMemory - afterMemory;

                                if (memoryFreed > 0)
                                {
                                    robloxProcessesTrimmed++;
                                    totalRobloxMemoryFreed += memoryFreed;

                                    App.Logger.WriteLine(LOG_IDENT_ROBLOX,
                                        $"Trimmed Roblox process {process.ProcessName}: {FormatBytes(memoryFreed)} freed");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT_ROBLOX, $"Error processing Roblox process: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_ROBLOX, $"Exception trimming Roblox processes: {ex.Message}");
            }
        }

        private bool TrimRobloxProcessMemory(Process process)
        {
            const string LOG_IDENT_ROBLOX_TRIM = $"{LOG_IDENT}::TrimRobloxProcessMemory";

            try
            {
                if (process.HasExited)
                    return false;

                if (!process.Responding)
                {
                    App.Logger.WriteLine(LOG_IDENT_ROBLOX_TRIM,
                        $"Roblox process {process.ProcessName} (PID: {process.Id}) is not responding");
                    return false;
                }

                bool success = true;

                try
                {
                    SetProcessWorkingSetSize(process.Handle, (IntPtr)(-1), (IntPtr)(-1));
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT_ROBLOX_TRIM,
                        $"SetProcessWorkingSetSize failed for {process.ProcessName}: {ex.Message}");
                    success = false;
                }

                try
                {
                    EmptyWorkingSet(process.Handle);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT_ROBLOX_TRIM,
                        $"EmptyWorkingSet failed for {process.ProcessName}: {ex.Message}");
                    success = false;
                }

                Thread.Sleep(50);

                return success;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_ROBLOX_TRIM,
                    $"Failed to trim Roblox process {process.ProcessName} (PID: {process.Id}): {ex.Message}");
                return false;
            }
        }

        public void TrimRobloxNow()
        {
            const string LOG_IDENT_MANUAL = $"{LOG_IDENT}::TrimRobloxNow";

            lock (_cleanupLock)
            {
                if (_isCleaning)
                {
                    App.Logger.WriteLine(LOG_IDENT_MANUAL, "Cleanup already in progress, skipping manual Roblox trim");
                    return;
                }

                _isCleaning = true;
            }

            try
            {
                App.Logger.WriteLine(LOG_IDENT_MANUAL, "Manual Roblox memory trim requested");
                TrimRobloxProcesses();
                OnRobloxTrimmed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_MANUAL, $"Exception during manual Roblox trim: {ex.Message}");
                OnCleanupError?.Invoke(this, ex.Message);
            }
            finally
            {
                lock (_cleanupLock)
                {
                    _isCleaning = false;
                }
            }
        }

        public void RefreshRobloxTimer()
        {
            UpdateRobloxTrimTimer();
        }

        private void CleanStandbyList()
        {
            const string LOG_IDENT_STANDBY = $"{LOG_IDENT}::CleanStandbyList";

            try
            {
                try
                {
                    using (var systemProcess = Process.GetProcessById(4))
                    {
                        if (!systemProcess.HasExited)
                        {
                            EmptyWorkingSet(systemProcess.Handle);
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT_STANDBY, $"System process method failed: {ex.Message}");
                }

                try
                {
                    IntPtr currentProcess = GetCurrentProcess();
                    SetProcessWorkingSetSize(currentProcess, (IntPtr)(-1), (IntPtr)(-1));
                    EmptyWorkingSet(currentProcess);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT_STANDBY, $"Current process method failed: {ex.Message}");
                }

                App.Logger.WriteLine(LOG_IDENT_STANDBY, "System standby list cleaned");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_STANDBY, $"Failed to clean standby list: {ex.Message}");
            }
        }

        private void CleanProcessWorkingSets()
        {
            const string LOG_IDENT_PROCESS = $"{LOG_IDENT}::CleanProcessWorkingSets";

            int cleanedProcesses = 0;
            int skippedProcesses = 0;

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (IsProcessSafeToClean(process))
                    {
                        ReduceProcessMemory(process);
                        cleanedProcesses++;
                    }
                    else
                    {
                        skippedProcesses++;
                    }
                }
                catch
                {
                    skippedProcesses++;
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private bool IsProcessSafeToClean(Process process)
        {
            try
            {
                if (process.HasExited || process.Id == Process.GetCurrentProcess().Id)
                    return false;

                if (!process.Responding)
                    return false;

                string processName = process.ProcessName.ToLower();

                if (_robloxProcessNames.Contains(processName))
                    return false;

                if (_criticalProcesses.Contains(processName))
                    return false;

                if (App.Settings.Prop.UserExcludedProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase))
                    return false;

                process.Refresh();

                if (process.WorkingSet64 < 50 * 1024 * 1024)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ReduceProcessMemory(Process process)
        {
            const string LOG_IDENT_REDUCE = $"{LOG_IDENT}::ReduceProcessMemory";

            try
            {
                if (!process.HasExited)
                {
                    long beforeMemory = process.WorkingSet64;

                    SetProcessWorkingSetSize(process.Handle, (IntPtr)(-1), (IntPtr)(-1));
                    EmptyWorkingSet(process.Handle);

                    process.Refresh();
                    long afterMemory = process.WorkingSet64;
                    long memoryFreed = beforeMemory - afterMemory;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_REDUCE, $"Failed to reduce memory for {process.ProcessName} (PID: {process.Id}): {ex.Message}");
            }
        }

        private long GetTotalMemoryUsage()
        {
            long total = 0;

            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Refresh();
                            total += process.WorkingSet64;
                        }
                    }
                    catch
                    {
                        // Ignore processes we can't access
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch { }

            return total;
        }

        private double GetIntervalMilliseconds(MemoryCleanerInterval interval)
        {
            return interval switch
            {
                MemoryCleanerInterval.TenMinutes => TimeSpan.FromMinutes(10).TotalMilliseconds,
                MemoryCleanerInterval.FifteenMinutes => TimeSpan.FromMinutes(15).TotalMilliseconds,
                MemoryCleanerInterval.TwentyMinutes => TimeSpan.FromMinutes(20).TotalMilliseconds,
                MemoryCleanerInterval.ThirtyMinutes => TimeSpan.FromMinutes(30).TotalMilliseconds,
                MemoryCleanerInterval.OneHour => TimeSpan.FromHours(1).TotalMilliseconds,
                _ => 0
            };
        }

        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n1} {suffixes[counter]}";
        }

        public void Dispose()
        {
            const string LOG_IDENT_DISPOSE = $"{LOG_IDENT}::Dispose";

            try
            {
                _cleanupTimer?.Stop();
                _cleanupTimer?.Dispose();
                _robloxTrimTimer?.Stop();
                _robloxTrimTimer?.Dispose();
                App.Logger.WriteLine(LOG_IDENT_DISPOSE, "Memory cleaner disposed");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_DISPOSE, $"Exception during dispose: {ex.Message}");
            }
        }

        public event EventHandler<long>? OnMemoryCleaned;
        public event EventHandler<string>? OnCleanupError;
        public event EventHandler? OnRobloxTrimmed;
    }
}