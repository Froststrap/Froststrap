// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;

namespace Froststrap.Clients.Launchers
{
    internal sealed class SoberLauncher(IClientLayout layout, string flatpakId) : IClientLauncher
    {
        private const string LatestLogName = "latest.log";
        private const int ReadyPollIntervalMs = 50;

        private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);

        public ReadySignalKind ReadySignal => ReadySignalKind.LogLine;

        public Task<LaunchResult> LaunchAsync(
            ClientInstall install,
            LaunchRequest request,
            CancellationToken cancellationToken = default)
        {
            ApplyLaunchSettings();

            KillRunning();

            string commandLine = request.CommandLine ?? string.Empty;
            App.Logger.Debug($"Launching Sober via flatpak with args: {commandLine}");

            var startInfo = new ProcessStartInfo
            {
                FileName = "flatpak",
                Arguments = $"run {flatpakId} {commandLine}",
                UseShellExecute = false,
            };

            var launchTime = DateTime.UtcNow;

            using var process = Process.Start(startInfo)!;

            App.Logger.Info($"Sober launched with PID {process.Id}");

            string latestLog = Path.Combine(install.LogsRoot!, LatestLogName);

            return Task.FromResult(new LaunchResult
            {
                Started = true,
                Pid = process.Id,
                ProcessName = layout.ProcessNames[0],
                LogHint = latestLog,
                WaitUntilReadyAsync = token => WaitForReadySignalAsync(latestLog, launchTime, token)
            });
        }

        private void KillRunning()
        {
            try
            {
                using Process? process = Process.Start(new ProcessStartInfo
                {
                    FileName = "flatpak",
                    Arguments = $"kill {flatpakId}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                process?.WaitForExit(1000);
            }
            catch
            {
            }
        }

        private static void ApplyLaunchSettings()
        {
            if (App.Settings.Prop.ShowServerDetails)
                App.SoberSettings.SetPreset("ServerLocationIndicatorEnabled", "false");

            if (App.Settings.Prop.UseDiscordRichPresence)
            {
                App.SoberSettings.SetPreset("DiscordRpcEnabled", "false");
                App.SoberSettings.SetPreset("DiscordRpcShowJoinButton", "false");
            }

            if (App.Settings.Prop.UseDisableAppPatch)
                App.SoberSettings.SetPreset("CloseOnLeave", "false");

            App.SoberSettings.Save();
        }

        private async Task WaitForReadySignalAsync(string latestLog, DateTime launchTime, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + ReadyTimeout;

            try
            {
                while (DateTime.UtcNow < deadline)
                {
                    if (File.Exists(latestLog) && File.GetLastWriteTimeUtc(latestLog) >= launchTime)
                        break;

                    await Task.Delay(ReadyPollIntervalMs, cancellationToken);
                }

                if (!File.Exists(latestLog))
                {
                    App.Logger.Info($"{LatestLogName} not found at {latestLog}, closing dialog.");
                    return;
                }

                App.Logger.Info($"Tailing {latestLog} for ready signal...");

                using var stream = new FileStream(latestLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);

                while (DateTime.UtcNow < deadline)
                {
                    string? line = await reader.ReadLineAsync(cancellationToken);

                    if (line is null)
                    {
                        await Task.Delay(ReadyPollIntervalMs, cancellationToken);
                        continue;
                    }

                    if (layout.ReadySignals.Any(signal => line.Contains(signal, StringComparison.Ordinal)))
                    {
                        App.Logger.Debug("Sober window ready, closing bootstrapper dialog.");
                        return;
                    }
                }

                App.Logger.Warn("Timed out waiting for Sober ready signal, closing dialog.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Log watcher error: {ex.Message} — closing dialog.");
            }
        }
    }
}
