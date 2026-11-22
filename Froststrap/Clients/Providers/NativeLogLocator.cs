// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;

namespace Froststrap.Clients.Providers;

/// <summary>
/// Windows and macOS clients: logs are named after the client and kind token, for example
/// <c>RobloxPlayer_20260101T000000Z_Player_abc123.log</c>.
/// </summary>
internal sealed class NativeLogLocator(IClientLayout layout, Func<string?> logsRoot) : LogLocatorBase(layout, logsRoot)
{
    public override bool IsGameLog(FileInfo file)
    {
        string? token = LogKindToken;

        if (string.IsNullOrEmpty(token))
            return false;

        if (!file.Name.Contains(token, StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (string excluded in ExcludedTokens)
        {
            if (file.Name.Contains(excluded, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public override FileInfo? FindNewer(FileInfo current)
    {
        if (Layout.Target != ClientTarget.Studio)
            return null;

        string? directory = current.DirectoryName;

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return null;

        try
        {
            return new DirectoryInfo(directory)
                .GetFiles()
                .Where(x => x.Name.Contains("Studio", StringComparison.OrdinalIgnoreCase))
                .Where(x => !x.Name.Contains("CrashHandler", StringComparison.OrdinalIgnoreCase))
                .Where(x => x.CreationTimeUtc > current.CreationTimeUtc)
                .OrderByDescending(x => x.CreationTimeUtc)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to search for a newer Studio log file: {ex.Message}");
            return null;
        }
    }
}
