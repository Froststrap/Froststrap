namespace Froststrap.Utility
{
    internal static class Shortcut
    {
        private static GenericTriState _loadStatus = GenericTriState.Unknown;

        public static async void Create(string exePath, string exeArgs, string lnkPath, string? iconPath = null)
        {
            const string LOG_IDENT = "Shortcut::Create";

            if (File.Exists(lnkPath))
                return;

            try
            {
                if (OperatingSystem.IsWindows())
                    CreateWindowsShortcut(exePath, exeArgs, lnkPath, iconPath);
                else if (OperatingSystem.IsMacOS())
                    CreateMacOSShortcut(exePath, exeArgs, lnkPath);
                else if (OperatingSystem.IsLinux())
                    CreateLinuxShortcut(exePath, exeArgs, lnkPath, iconPath);

                if (_loadStatus != GenericTriState.Successful)
                    _loadStatus = GenericTriState.Successful;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to create a shortcut for {lnkPath}!");
                App.Logger.WriteException(LOG_IDENT, ex);

                if (_loadStatus == GenericTriState.Failed)
                    return;

                _loadStatus = GenericTriState.Failed;

                await Frontend.ShowMessageBox(Strings.Dialog_CannotCreateShortcuts, MessageBoxImage.Warning);
            }
        }

        private static void CreateWindowsShortcut(string exePath, string exeArgs, string lnkPath, string? iconPath)
        {
            string finalIconPath = string.IsNullOrEmpty(iconPath) ? exePath : iconPath;
            ShellLink.Shortcut.CreateShortcut(exePath, exeArgs, finalIconPath, 0).WriteToFile(lnkPath);
        }

        // idk if these will work at all
        private static void CreateMacOSShortcut(string exePath, string exeArgs, string appBundlePath)
        {
            if (!appBundlePath.EndsWith(".app"))
                appBundlePath += ".app";

            string contentsDir = Path.Combine(appBundlePath, "Contents");
            string macOSDir = Path.Combine(contentsDir, "MacOS");

            Directory.CreateDirectory(macOSDir);

            string scriptPath = Path.Combine(macOSDir, "launcher");
            File.WriteAllText(scriptPath,
                $"""
                #!/bin/bash
                exec "{exePath}" {exeArgs}
                """);

            Process.Start("chmod", $"+x \"{scriptPath}\"")?.WaitForExit();

            File.WriteAllText(Path.Combine(contentsDir, "Info.plist"),
                $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
                    "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>CFBundleExecutable</key>  <string>launcher</string>
                    <key>CFBundleIdentifier</key>  <string>com.froststrap.shortcut</string>
                    <key>CFBundleName</key>         <string>{Path.GetFileNameWithoutExtension(appBundlePath)}</string>
                    <key>CFBundleVersion</key>      <string>1.0</string>
                </dict>
                </plist>
                """);
        }

        private static void CreateLinuxShortcut(string exePath, string exeArgs, string desktopPath, string? iconPath)
        {
            if (!desktopPath.EndsWith(".desktop"))
                desktopPath += ".desktop";

            string appName = Path.GetFileNameWithoutExtension(desktopPath);
            string icon = string.IsNullOrEmpty(iconPath) ? "application-x-executable" : iconPath;

            File.WriteAllText(desktopPath,
                $"""
                [Desktop Entry]
                Type=Application
                Name={appName}
                Exec="{exePath}" {exeArgs}
                Icon={icon}
                Terminal=false
                """);

            System.Diagnostics.Process.Start("chmod", $"+x \"{desktopPath}\"")?.WaitForExit();
        }
    }
}