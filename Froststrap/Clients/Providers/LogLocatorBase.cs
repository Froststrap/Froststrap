// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;

namespace Froststrap.Clients.Providers;

/// <summary>
/// Shared log discovery. Owns directory, filename rules, creation-time window, and
/// rotation. Line parsing stays in the watcher.
/// </summary>
internal abstract class LogLocatorBase(IClientLayout layout, Func<string?> logsRoot) : ILogLocator
{
    protected IClientLayout Layout { get; } = layout;

    public string? LogsRoot => logsRoot();

    public LogShape LogFormat => Layout.LogFormat;

    public string? LogKindToken => Layout.LogKindToken;

    public IReadOnlyList<string> ReadySignals => Layout.ReadySignals;

    public IReadOnlyList<string> ExcludedTokens => Layout.ExcludedLogTokens;

    public abstract bool IsGameLog(FileInfo file);

    public virtual async Task<FileInfo?> FindLatestAsync(DateTime notBefore, CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? root = LogsRoot;

            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                try
                {
                    FileInfo? newest = new DirectoryInfo(root)
                        .GetFiles()
                        .Where(IsGameLog)
                        .Where(x => x.CreationTime >= notBefore)
                        .OrderByDescending(x => x.CreationTime)
                        .FirstOrDefault();

                    if (newest is not null)
                        return newest;
                }
                catch (IOException ex)
                {
                    App.Logger.Error($"Failed to scan log directory: {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    App.Logger.Error($"Failed to scan log directory: {ex.Message}");
                }
            }

            string kind = LogKindToken?.Trim('_') ?? Layout.ClientId;
            App.Logger.Info($"No recent '{kind}' log file yet, waiting 1000...");
            await Task.Delay(1000, cancellationToken);
        }

        return null;
    }

    public abstract FileInfo? FindNewer(FileInfo current);
}
