using System.Runtime.Versioning;

namespace Froststrap.Utility;

[SupportedOSPlatform("linux")]
internal static class LinuxRegistry
{
    private static readonly string DesktopEntryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "applications"
    );

    private static readonly string DesktopFilePath = Path.Combine(
        DesktopEntryDir, "froststrap-handler.desktop"
    );

    private static readonly string MimePackagesDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "mime", "packages"
    );

    private static readonly string MimeXmlPath = Path.Combine(
        MimePackagesDir, "froststrap-mime.xml"
    );

    private static readonly string[] Schemes =
    [
        "roblox",
        "roblox-player",
        "roblox-studio",
        "roblox-studio-auth"
    ];

    private static readonly Dictionary<string, string> ExtensionToMime = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".rbxl", "application/x-roblox-place" },
        { ".rbxlx", "application/x-roblox-place" },
        { ".rbxm", "application/x-roblox-model" },
        { ".rbxmx", "application/x-roblox-model" }
    };

    public static void RegisterAll()
    {
        if (!OperatingSystem.IsLinux()) return;

        Directory.CreateDirectory(DesktopEntryDir);

        var sb = new StringBuilder();
        sb.AppendLine("[Desktop Entry]");
        sb.AppendLine("Type=Application");
        sb.AppendLine($"Name={App.ProjectName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Exec={Paths.Application} %u");
        sb.AppendLine("StartupNotify=true");
        sb.AppendLine("Terminal=false");
        var allMimeTypes = Schemes.Select(s => $"x-scheme-handler/{s}")
            .Concat(ExtensionToMime.Values.Distinct())
            .ToList();
        sb.AppendLine("MimeType=" + string.Join(";", allMimeTypes));
        sb.AppendLine("NoDisplay=true");

        File.WriteAllText(DesktopFilePath, sb.ToString());

        Process.Start("chmod", $"+x \"{DesktopFilePath}\"")?.WaitForExit();

        RegisterMimeTypes();

        try
        {
            Process.Start("update-desktop-database", DesktopEntryDir)?.WaitForExit();
        }
        catch { }

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
            catch { }
        }

        foreach (var mime in ExtensionToMime.Values.Distinct())
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "xdg-mime",
                    Arguments = $"default froststrap-handler.desktop {mime}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                using var p = Process.Start(psi);
                p?.WaitForExit();
            }
            catch { }
        }
    }

    private static void RegisterMimeTypes()
    {
        if (!OperatingSystem.IsLinux()) return;

        Directory.CreateDirectory(MimePackagesDir);

        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<mime-info xmlns=\"http://www.freedesktop.org/standards/shared-mime-info\">");

        foreach (var kv in ExtensionToMime)
        {
            string ext = kv.Key.TrimStart('.');
            string mime = kv.Value;
            xml.AppendLine(CultureInfo.InvariantCulture, $"  <mime-type type=\"{mime}\">");
            xml.AppendLine(CultureInfo.InvariantCulture, $"    <comment>Roblox Studio {kv.Key} file</comment>");
            xml.AppendLine(CultureInfo.InvariantCulture, $"    <glob pattern=\"*{kv.Key}\"/>");
            xml.AppendLine($"  </mime-type>");
        }

        xml.AppendLine("</mime-info>");

        File.WriteAllText(MimeXmlPath, xml.ToString());

        try
        {
            Process.Start("update-mime-database", MimePackagesDir)?.WaitForExit();
        }
        catch { }
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
            catch { }
        }

        if (File.Exists(MimeXmlPath))
        {
            File.Delete(MimeXmlPath);
            try
            {
                Process.Start("update-mime-database", MimePackagesDir)?.WaitForExit();
            }
            catch { }
        }
    }
}