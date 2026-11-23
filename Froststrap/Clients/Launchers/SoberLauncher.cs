// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;
using Froststrap.Clients.Providers;

namespace Froststrap.Clients.Launchers;

internal sealed class SoberLauncher(IClientLayout layout, FlatpakProcessService processes, ISettingsSync settings) : IClientLauncher
{
    private const string LatestLogName = "latest.log";
    private const int ReadyPollIntervalMs = 50;

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);

    public Task<LaunchResult> LaunchAsync(
        ClientInstall install,
        LaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ApplyLaunchSettings();

        processes.KillApp();

        string commandLine = request.CommandLine ?? string.Empty;

        if (request.JoinData is not null && BuildSoberUri(request.JoinData) is { } soberUri)
        {
            App.Logger.Info($"Translating launch command for Sober: {soberUri}");
            commandLine = soberUri;
        }
        App.Logger.Debug($"Launching Sober via flatpak with args: {commandLine}");

        var startInfo = new ProcessStartInfo
        {
            FileName = "flatpak",
            Arguments = $"run {processes.FlatpakId} {commandLine}",
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
            WaitUntilReadyAsync = token => WaitForReadySignalAsync(latestLog, launchTime, token)
        });
    }

    /// Rebuilds parsed launch data into a <c>roblox://experiences/start</c> deeplink,
    /// the only launch format Sober understands. Returns null when there is nothing
    /// to join, so the original launch command is left untouched.
    private static string? BuildSoberUri(GameJoinData joinData)
    {
        if (joinData.PlaceId is not long placeId || placeId <= 0)
            return null;

        bool isPlayTogether = joinData.JoinType == GameJoinType.RequestPlayTogetherGame;
        string uri = $"roblox://experiences/start?placeId={placeId}";

        if (joinData.AccessCode is not null)
            uri += $"&accessCode={joinData.AccessCode}";
        else if (!isPlayTogether && joinData.JobId is not null)
            uri += $"&gameInstanceId={joinData.JobId}";
        return uri;
    }

    private void ApplyLaunchSettings()
    {
        if (App.Settings.Prop.EnableActivityTracking && App.Settings.Prop.ShowServerDetails)
            settings.SetPreset("ServerLocationIndicatorEnabled", "false");

        if (App.Settings.Prop.EnableActivityTracking && App.Settings.Prop.UseDiscordRichPresence)
        {
            settings.SetPreset("DiscordRpcEnabled", "false");
            settings.SetPreset("DiscordRpcShowJoinButton", "false");
        }

        if (App.Settings.Prop.EnableActivityTracking && App.Settings.Prop.UseDisableAppPatch)
            settings.SetPreset("CloseOnLeave", "false");

        settings.Save();
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
