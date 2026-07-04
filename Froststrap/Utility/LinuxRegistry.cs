using System.Text;
using System.Runtime.Versioning;

namespace Froststrap.Utility;

[SupportedOSPlatform("linux")]
public static class LinuxRegistry
{
    private static readonly string DesktopEntryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "applications"
    );

    private static readonly string DesktopFilePath = Path.Combine(
        DesktopEntryDir, "froststrap-handler.desktop"
    );

    private static readonly string[] Schemes =
    [
        "roblox",
        "roblox-player",
        "roblox-studio",
        "roblox-studio-auth"
    ];

    public static void RegisterAll()
    {
        if (!OperatingSystem.IsLinux()) return;

        Directory.CreateDirectory(DesktopEntryDir);

        var sb = new StringBuilder();
        sb.AppendLine("[Desktop Entry]");
        sb.AppendLine("Type=Application");
        sb.AppendLine($"Name={App.ProjectName}");
        sb.AppendLine($"Exec={Paths.Application} %u");
        sb.AppendLine("StartupNotify=true");
        sb.AppendLine("Terminal=false");
        sb.AppendLine("MimeType=" + string.Join(";", Schemes.Select(s => $"x-scheme-handler/{s}")));
        sb.AppendLine("NoDisplay=true");

        File.WriteAllText(DesktopFilePath, sb.ToString());

        Process.Start("chmod", $"+x \"{DesktopFilePath}\"")?.WaitForExit();

        try
        {
            Process.Start("update-desktop-database", DesktopEntryDir)?.WaitForExit();
        }
        catch
        {
        }

        foreach (var scheme in Schemes)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "xdg-mime",
                    Arguments = $"default froststrap-handler.desktop x-scheme-handler/{scheme}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                using var p = Process.Start(psi);
                p?.WaitForExit();
            }
            catch
            {
            }
        }
    }

    public static void UnregisterAll()
    {
        if (!OperatingSystem.IsLinux()) return;

        if (File.Exists(DesktopFilePath))
        {
            File.Delete(DesktopFilePath);
            try
            {
                Process.Start("update-desktop-database", DesktopEntryDir)?.WaitForExit();
            }
            catch
            {
            }
        }
    }
}