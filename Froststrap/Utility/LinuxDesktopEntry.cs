// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Utility;

internal static class LinuxDesktopEntry
{
    public const string AppId = "com.froststrap.Froststrap";
    public const string FileName = AppId + ".desktop";

    private static readonly Lock _lock = new();
    private static bool _installed;

    public static string ApplicationsDirectory =>
        Path.Combine(Paths.UserProfile, ".local", "share", "applications");

    public static string FullPath => Path.Combine(ApplicationsDirectory, FileName);

    public static void EnsureInstalled()
    {
        if (!OperatingSystem.IsLinux())
            return;

        if (_installed)
            return;

        lock (_lock)
        {
            if (_installed)
                return;

            _installed = true;

            try
            {
                Directory.CreateDirectory(ApplicationsDirectory);

                string executable = Environment.GetEnvironmentVariable("APPIMAGE")
                    ?? Environment.ProcessPath
                    ?? "froststrap";

                string content =
                    $"""
                     [Desktop Entry]
                     Type=Application
                     Name=Froststrap
                     Comment=Froststrap - A Fishstrap fork
                     Exec="{executable}" %u
                     Icon=froststrap
                     Terminal=false
                     Categories=Utility;Game;
                     StartupNotify=true
                     StartupWMClass=Froststrap
                     DBusActivatable=false
                     MimeType=x-scheme-handler/roblox;x-scheme-handler/roblox-player;x-scheme-handler/roblox-studio;x-scheme-handler/roblox-studio-auth;
                     """;

                bool changed = !File.Exists(FullPath)
                    || !string.Equals(File.ReadAllText(FullPath), content, StringComparison.Ordinal);

                if (changed)
                    File.WriteAllText(FullPath, content);

                Environment.SetEnvironmentVariable("BAMF_DESKTOP_FILE_HINT", FileName);
                Environment.SetEnvironmentVariable("CHROME_DESKTOP", FileName);
                Environment.SetEnvironmentVariable("XDG_DESKTOP_PORTAL_APP_ID", AppId);

                if (changed)
                    RefreshDesktopDatabase();
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Could not install the Linux desktop entry: {ex.Message}");
            }
        }
    }

    private static void RefreshDesktopDatabase()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("update-desktop-database", ApplicationsDirectory)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            });

            process?.WaitForExit(3000);
        }
        catch
        {
        }
    }
}