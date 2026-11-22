// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Sources;

internal sealed partial class FlatpakSource(string appId)
{
    private static readonly TimeSpan FlatpakCheckTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromMinutes(20);

    public static async Task EnsureFlatpakAvailableAsync()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "flatpak",
            Arguments = "--version",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start flatpak process.");

        await process.WaitForExitAsync().WaitAsync(FlatpakCheckTimeout);

        if (process.ExitCode != 0)
            throw new InvalidOperationException("Flatpak returned a non-zero exit code.");
    }

    public async Task<bool> IsInstalledAsync()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "flatpak",
            Arguments = $"info {appId}",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo);

            if (process is null)
                return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, $"Failed to check {appId} installation status.");
            return false;
        }
    }

    public async Task<InstallResult> InstallAsync(IProgress<InstallProgress>? progress = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "flatpak",
            Arguments = $"install --noninteractive flathub {appId}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);

        if (process is null)
            return new InstallResult { Success = false };

        var errorLines = new List<string>();

        async Task ReadStream(StreamReader reader)
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                App.Logger.Info($"[flatpak] {line}");

                string? step = ParseInstallStep(line);

                if (!string.IsNullOrEmpty(step))
                    progress?.Report(new InstallProgress(InstallPhase.Downloading, step));
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    lock (errorLines)
                        errorLines.Add(line.Trim());
                }
            }
        }

        await Task.WhenAll(
            ReadStream(process.StandardOutput),
            ReadStream(process.StandardError),
            process.WaitForExitAsync());

        if (process.ExitCode == 0)
            return new InstallResult { Success = true };

        return new InstallResult
        {
            Success = false,
            Error = string.Join('\n', errorLines.TakeLast(8))
        };
    }

    public async Task UpdateAsync(IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        App.Logger.Info($"Running 'flatpak update {appId}'.");

        var startInfo = new ProcessStartInfo
        {
            FileName = "flatpak",
            Arguments = $"update --user {appId} --assumeyes",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var timeout = new CancellationTokenSource(UpdateTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start flatpak update process.");

            var outputTask = Task.Run(async () =>
            {
                try
                {
                    while (!linked.Token.IsCancellationRequested)
                    {
                        string? line = await process.StandardOutput.ReadLineAsync();

                        if (line is null)
                            break;

                        if (!string.IsNullOrWhiteSpace(line))
                            ReportUpdateLine(line, progress);
                    }
                }
                catch (OperationCanceledException)
                {
                    App.Logger.Info("Output reading cancelled.");
                }
                catch (Exception ex)
                {
                    App.Logger.Warn($"Error reading flatpak output: {ex.Message}");
                }
            }, linked.Token);

            var errorTask = Task.Run(async () =>
            {
                try
                {
                    while (!linked.Token.IsCancellationRequested)
                    {
                        string? line = await process.StandardError.ReadLineAsync();

                        if (line is null)
                            break;

                        if (!string.IsNullOrWhiteSpace(line))
                            App.Logger.Warn($"[flatpak-err] {line}");
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    App.Logger.Warn($"Error reading flatpak stderr: {ex.Message}");
                }
            }, linked.Token);

            await Task.WhenAny(
                process.WaitForExitAsync(linked.Token),
                Task.Delay(Timeout.Infinite, linked.Token));

            if (!process.HasExited && linked.IsCancellationRequested)
            {
                App.Logger.Warn("Update cancelled by user or timeout. Killing process.");
                try { process.Kill(true); } catch { }
            }

            if (!process.HasExited)
                await process.WaitForExitAsync(CancellationToken.None);

            if (process.ExitCode != 0 && process.ExitCode != -1)
            {
                App.Logger.Warn($"flatpak update exited with code {process.ExitCode}.");
            }
            else if (process.ExitCode == 0)
            {
                App.Logger.Info($"{appId} update finished successfully.");
                progress?.Report(new InstallProgress(InstallPhase.Finalizing, string.Empty, 100, 100));
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            App.Logger.Warn("Update timed out after 20 minutes.");
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, $"Failed to update {appId}.");
        }
    }

    private static void ReportUpdateLine(string line, IProgress<InstallProgress>? progress)
    {
        App.Logger.Info($"[flatpak] {line}");

        Match stepMatch = UpdateStepRegex().Match(line);
        Match percentMatch = PercentRegex().Match(line);

        if (stepMatch.Success)
        {
            int current = int.Parse(stepMatch.Groups["current"].Value, CultureInfo.InvariantCulture);
            int total = int.Parse(stepMatch.Groups["total"].Value, CultureInfo.InvariantCulture);
            double segmentSize = 100.0 / total;

            if (percentMatch.Success)
            {
                int percent = int.Parse(percentMatch.Groups["percent"].Value, CultureInfo.InvariantCulture);
                int overall = Math.Clamp((int)Math.Round((current - 1) * segmentSize + (percent / 100.0) * segmentSize), 0, 100);

                progress?.Report(new InstallProgress(InstallPhase.Downloading, line, overall, 100, current, total, percent));
            }
            else
            {
                int overall = Math.Clamp((int)Math.Round((current - 1) * segmentSize), 0, 100);

                progress?.Report(new InstallProgress(InstallPhase.Downloading, line, overall, 100, current, total));
            }

            return;
        }

        string trimmed = line.Trim();

        if (!string.IsNullOrEmpty(trimmed) && trimmed.Length < 80)
            progress?.Report(new InstallProgress(InstallPhase.Preparing, trimmed));
    }

    private static string? ParseInstallStep(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        line = AnsiEscapeRegex().Replace(line, string.Empty).Replace('\r', ' ').Trim();

        int installingIndex = line.IndexOf("Installing", StringComparison.OrdinalIgnoreCase);

        return installingIndex < 0 ? null : line[installingIndex..].Trim();
    }

    [GeneratedRegex(@"Updating\s+(?<current>\d+)/(?<total>\d+)…", RegexOptions.IgnoreCase)]
    private static partial Regex UpdateStepRegex();

    [GeneratedRegex(@"(?<percent>\d+)%")]
    private static partial Regex PercentRegex();

    [GeneratedRegex(@"\x1B\[[0-9;?]*[ -/]*[@-~]")]
    private static partial Regex AnsiEscapeRegex();
}
