using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Froststrap
{
    internal static class UriHandler
    {
        static readonly string[] Schemes =
        [
            "roblox",
            "roblox-player",
            "roblox-studio",
            "roblox-studio-auth"
        ];

        static readonly IHandler? Handler =
            OperatingSystem.IsWindows() ? new WindowsHandler() :
            OperatingSystem.IsLinux() ? new LinuxHandler() :
            null; // macOS registers through the app bundle's Info.plist

        public static void RegisterProtocolHandlers() => Safely(() => Handler?.Register());

        public static void UnregisterProtocolHandlers() => Safely(() => Handler?.Unregister());

        public static void EnsureRegistered() => Safely(() =>
        {
            if (Handler is not null && !Handler.IsRegistered())
                Handler.Register();
        });

        // Velopack no likey likey excptions in install/uninstall phase
        // So let's handle it properly
        static void Safely(Action action)
        {
            try { action(); }
            catch (Exception ex) { App.Logger.Error(ex, "UriHandler failed"); }
        }

        interface IHandler
        {
            void Register();
            void Unregister();
            bool IsRegistered();
        }

        [SupportedOSPlatform("windows")]
        sealed class WindowsHandler : IHandler
        {
            public void Register()
            {
                string exe = Environment.ProcessPath!;
                foreach (string scheme in Schemes)
                {
                    using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{scheme}");
                    key.SetValue("", $"URL: {scheme} Protocol");
                    key.SetValue("URL Protocol", "");
                    using var command = key.CreateSubKey(@"shell\open\command");
                    command.SetValue("", $"\"{exe}\" \"%1\"");
                }
            }

            public void Unregister()
            {
                foreach (string scheme in Schemes)
                    Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{scheme}", throwOnMissingSubKey: false);
            }

            public bool IsRegistered()
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\roblox-player\shell\open\command");
                return key?.GetValue("") as string == $"\"{Environment.ProcessPath}\" \"%1\"";
            }
        }

        [SupportedOSPlatform("linux")]
        sealed class LinuxHandler : IHandler
        {
            static string DesktopFile => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "applications", "froststrap.desktop");

            // Inside an AppImage, ProcessPath is a temporary mount, APPIMAGE is the real file.
            static string ExePath => Environment.GetEnvironmentVariable("APPIMAGE") ?? Environment.ProcessPath!;

            public void Register()
            {
                string mime = string.Concat(Schemes.Select(s => $"x-scheme-handler/{s};"));

                Directory.CreateDirectory(Path.GetDirectoryName(DesktopFile)!);
                File.WriteAllText(DesktopFile,
                    $"[Desktop Entry]\nType=Application\nName=Froststrap\nExec=\"{ExePath}\" %u\nTerminal=false\nNoDisplay=true\nMimeType={mime}\n");

                foreach (string scheme in Schemes)
                    Run("xdg-mime", $"default froststrap.desktop x-scheme-handler/{scheme}");
                Run("update-desktop-database", $"\"{Path.GetDirectoryName(DesktopFile)}\"");
            }

            public void Unregister()
            {
                if (File.Exists(DesktopFile)) File.Delete(DesktopFile);
            }

            public bool IsRegistered()
            {
                if (!File.Exists(DesktopFile)) return false;
                if (!File.ReadAllText(DesktopFile).Contains($"Exec=\"{ExePath}\"", StringComparison.Ordinal)) return false;

                using var query = Process.Start(new ProcessStartInfo("xdg-mime", "query default x-scheme-handler/roblox-player")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });
                string current = query?.StandardOutput.ReadToEnd().Trim() ?? "";
                query?.WaitForExit(5000);
                return current == "froststrap.desktop";
            }

            static void Run(string file, string args)
            {
                try { Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false })?.WaitForExit(5000); }
                catch { /* tool may be missing, not fatal */ }
            }
        }
    }
}
