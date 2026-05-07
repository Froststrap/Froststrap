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

        private static string DesktopFilePath => Path.Combine(DesktopFileDir, "froststrap.desktop");
        private const string DesktopFileName = "froststrap.desktop";

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
                   "Name=Froststrap\n" +
                   "Comment=Roblox bootstrapper and mod manager\n" +
                   $"Exec=\"{exePath}\" -player \"%u\"\n" +
                   "Icon=froststrap\n" +
                   "Terminal=false\n" +
                   "Categories=Game;\n" +
                   "MimeType=x-scheme-handler/roblox;x-scheme-handler/roblox-player;\n";

                File.WriteAllText(DesktopFilePath, desktopContent);

                RunCommand("chmod", $"+x \"{DesktopFilePath}\"");

                foreach (string scheme in new[] { "x-scheme-handler/roblox", "x-scheme-handler/roblox-player" })
                {
                    RunCommand("xdg-mime", $"default {DesktopFileName} {scheme}");
                }

                // Chromium and portal-based launchers may consult xdg-settings for scheme handlers.
                RunCommand("xdg-settings", $"set default-url-scheme-handler roblox {DesktopFileName}");
                RunCommand("xdg-settings", $"set default-url-scheme-handler roblox-player {DesktopFileName}");

                RunCommand("update-desktop-database", DesktopFileDir);

                App.Logger.WriteLine(LOG_IDENT, "Registered roblox:// and roblox-player:// URI handlers via xdg-mime and xdg-settings.");
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
                    App.Logger.WriteLine(LOG_IDENT, "Deleted froststrap.desktop");
                }

                RunCommand("update-desktop-database", DesktopFileDir);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to unregister Linux URI protocol handlers.");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        private static void RunCommand(string fileName, string arguments)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                System.Diagnostics.Process.Start(startInfo)?.WaitForExit();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("LinuxProtocolHandler::RunCommand", $"Failed to run '{fileName} {arguments}'");
                App.Logger.WriteException("LinuxProtocolHandler::RunCommand", ex);
            }
        }
    }
}
