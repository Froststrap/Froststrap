using System.Runtime.Versioning;

// TODO: clean this up
namespace Froststrap.Utility
{
    [SupportedOSPlatform("linux")]
    internal static class LinuxProtocolHandler
    {
        private static string DesktopFileDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "applications"
        );

        private static string DesktopFilePath => Path.Combine(DesktopFileDir, "froststrap-uri-handler.desktop");

        public static void RegisterProtocols()
        {
            const string LOG_IDENT = "LinuxProtocolHandler::RegisterProtocols";

            try
            {
                Directory.CreateDirectory(DesktopFileDir);

                string exePath = Environment.GetEnvironmentVariable("APPIMAGE") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                    exePath = Paths.Process;

                string desktopContent =
                   "[Desktop Entry]\n" +
                   "Type=Application\n" +
                   "Name=Froststrap URI Handler\n" +
                   $"Exec=\"{exePath}\" -player \"%u\"\n" +
                   "NoDisplay=true\n" +
                   "MimeType=x-scheme-handler/roblox;x-scheme-handler/roblox-player;\n";

                File.WriteAllText(DesktopFilePath, desktopContent);

                var chmodInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{DesktopFilePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(chmodInfo)?.WaitForExit();

                foreach (string scheme in new[] { "x-scheme-handler/roblox", "x-scheme-handler/roblox-player" })
                {
                    var xdgMime = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "xdg-mime",
                        Arguments = $"default froststrap-uri-handler.desktop {scheme}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    System.Diagnostics.Process.Start(xdgMime)?.WaitForExit();
                }

                var updateDb = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "update-desktop-database",
                    Arguments = DesktopFileDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(updateDb)?.WaitForExit();

                App.Logger.WriteLine(LOG_IDENT, "Registered roblox:// and roblox-player:// URI handlers via xdg-mime.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to register Linux URI protocol handlers.");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public static void UnregisterProtocols()
        {
            const string LOG_IDENT = "LinuxProtocolHandler::UnregisterProtocols";

            try
            {
                if (File.Exists(DesktopFilePath))
                {
                    File.Delete(DesktopFilePath);
                    App.Logger.WriteLine(LOG_IDENT, "Deleted froststrap-uri-handler.desktop");
                }

                var updateDb = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "update-desktop-database",
                    Arguments = DesktopFileDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(updateDb)?.WaitForExit();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to unregister Linux URI protocol handlers.");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }
    }
}
