/*
*  Froststrap
*  Copyright (c) Froststrap Team
*
*  This file is part of Froststrap and is distributed under the terms of the
*  GNU Affero General Public License, version 3 or later.
*
*  SPDX-License-Identifier: AGPL-3.0-or-later
*/

using System.Security.Cryptography;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Froststrap.Utility
{
    internal static class Shortcut
    {
        private static GenericTriState _loadStatus = GenericTriState.Unknown;

        private static string? _froststrapIconPath;
        private static readonly Lock _iconLock = new();

        public static string GetFroststrapIconPath()
        {
            if (_froststrapIconPath != null && File.Exists(_froststrapIconPath))
                return _froststrapIconPath;

            lock (_iconLock)
            {
                if (_froststrapIconPath != null && File.Exists(_froststrapIconPath))
                    return _froststrapIconPath;

                try
                {
                    string iconPath = Path.Combine(Paths.Base, "froststrap.png");

                    if (File.Exists(iconPath))
                    {
                        _froststrapIconPath = iconPath;
                        return iconPath;
                    }

                    var uri = new Uri("avares://Froststrap/Froststrap.png");
                    using var pngStream = AssetLoader.Open(uri);
                    if (pngStream is null)
                        throw new FileNotFoundException("Embedded Froststrap.png not found.");

                    using var fileStream = File.Create(iconPath);
                    pngStream.CopyTo(fileStream);

                    _froststrapIconPath = iconPath;
                    return iconPath;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("Shortcut::GetFroststrapIconPath", $"Failed to extract icon: {ex.Message}");
                    return "application-x-executable";
                }
            }
        }

        public static string ResolvePath(string lnkPath)
        {
            if (OperatingSystem.IsLinux())
                return Path.ChangeExtension(lnkPath, ".desktop");
            if (OperatingSystem.IsMacOS() && !lnkPath.EndsWith(".app"))
                return lnkPath + ".app";
            return lnkPath;
        }

        public static async void Create(string exePath, string exeArgs, string lnkPath, string? iconPath = null)
        {
            const string LOG_IDENT = "Shortcut::Create";
            string resolvedPath = ResolvePath(lnkPath);

            if (File.Exists(resolvedPath))
                return;

            try
            {
                if (OperatingSystem.IsWindows())
                    CreateWindowsShortcut(exePath, exeArgs, lnkPath, iconPath);
                else if (OperatingSystem.IsMacOS())
                    CreateMacOsShortcut(exePath, exeArgs, lnkPath);
                else if (OperatingSystem.IsLinux())
                    CreateLinuxShortcut(exePath, exeArgs, lnkPath, iconPath);

                if (_loadStatus != GenericTriState.Successful)
                    _loadStatus = GenericTriState.Successful;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to create a shortcut for {resolvedPath}!");
                App.Logger.WriteException(LOG_IDENT, ex);

                if (_loadStatus == GenericTriState.Failed)
                    return;

                _loadStatus = GenericTriState.Failed;

                await Frontend.ShowMessageBox(Strings.Dialog_CannotCreateShortcuts, MessageBoxImage.Warning);
            }
        }
        
        public static void Delete(string lnkPath)
        {
            // Always try to clean up the raw path too in case an old .lnk was left behind.
            if (File.Exists(lnkPath))
                File.Delete(lnkPath);

            string resolvedPath = ResolvePath(lnkPath);
            if (File.Exists(resolvedPath))
                File.Delete(resolvedPath);

            if (OperatingSystem.IsLinux())
            {
                string appMenuPath = Path.Combine(
                    GetLinuxAppMenuDir(),
                    Path.GetFileName(resolvedPath)
                );
                if (File.Exists(appMenuPath))
                    File.Delete(appMenuPath);
            }
        }

        public static async Task CreateGameShortcut(
            string appPath,
            string placeId,
            string? jobId,
            string? accessCode,
            Bitmap? icon,
            Action<string>? onStatus = null)
        {
            const string LOG_IDENT = "Shortcut::CreateGameShortcut";

            string argData = placeId;
            if (!string.IsNullOrEmpty(jobId)) argData += $";{jobId}";
            if (!string.IsNullOrEmpty(accessCode)) argData += $";{accessCode}";

            string safeName = SanitizeFileName(placeId); // caller should pass display name; kept generic here
            string lnkPath = Path.Combine(Paths.Desktop, $"{safeName}.lnk");

            string? finalIconPath = null;

            if (icon != null)
            {
                try
                {
                    onStatus?.Invoke("Saving icon...");

                    string shortcutsIconDir = Path.Combine(Paths.Cache, "Game Shortcuts");
                    Directory.CreateDirectory(shortcutsIconDir);

                    using var ms = new MemoryStream();
                    icon.Save(ms);
                    byte[] imageBytes = ms.ToArray();

                    string hash = ComputeHash(imageBytes);
                    string icoPath = Path.Combine(shortcutsIconDir, $"{hash}.ico");

                    if (!File.Exists(icoPath))
                    {
                        onStatus?.Invoke("Converting icon...");
                        using var icoFile = File.Create(icoPath);
                        SaveBitmapAsIcon(icon, icoFile);
                    }

                    finalIconPath = icoPath;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Icon processing failed: {ex.Message}");
                }
            }

            onStatus?.Invoke("Creating...");
            Create(appPath, $"-gameshortcut \"{argData}\"", lnkPath, finalIconPath);
        }

        public static Task CreateGameShortcut(
            string appPath,
            string displayName,
            string placeId,
            string? jobId,
            string? accessCode,
            Bitmap? icon,
            Action<string>? onStatus = null)
        {
            string safeName = SanitizeFileName(displayName);
            string lnkPath = Path.Combine(Paths.Desktop, $"{safeName}.lnk");

            // Delegate with the resolved lnk path baked in via a local wrapper.
            return CreateGameShortcutInternal(appPath, placeId, jobId, accessCode, icon, lnkPath, onStatus);
        }

        private static async Task CreateGameShortcutInternal(
            string appPath,
            string placeId,
            string? jobId,
            string? accessCode,
            Bitmap? icon,
            string lnkPath,
            Action<string>? onStatus)
        {
            const string LOG_IDENT = "Shortcut::CreateGameShortcutInternal";

            string argData = placeId;
            if (!string.IsNullOrEmpty(jobId)) argData += $";{jobId}";
            if (!string.IsNullOrEmpty(accessCode)) argData += $";{accessCode}";

            string? finalIconPath = null;

            if (icon != null)
            {
                try
                {
                    onStatus?.Invoke("Saving icon...");

                    string shortcutsIconDir = Path.Combine(Paths.Cache, "Game Shortcuts");
                    Directory.CreateDirectory(shortcutsIconDir);

                    using var ms = new MemoryStream();
                    icon.Save(ms);
                    byte[] imageBytes = ms.ToArray();

                    string hash = ComputeHash(imageBytes);
                    string icoPath = Path.Combine(shortcutsIconDir, $"{hash}.ico");

                    if (!File.Exists(icoPath))
                    {
                        onStatus?.Invoke("Converting icon...");
                        using var icoFile = File.Create(icoPath);
                        SaveBitmapAsIcon(icon, icoFile);
                    }

                    finalIconPath = icoPath;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Icon processing failed: {ex.Message}");
                }
            }

            onStatus?.Invoke("Creating...");
            Create(appPath, $"-gameshortcut \"{argData}\"", lnkPath, finalIconPath);
        }

        private static void CreateWindowsShortcut(string exePath, string exeArgs, string lnkPath, string? iconPath)
        {
            string finalIconPath = string.IsNullOrEmpty(iconPath) ? exePath : iconPath;
            ShellLink.Shortcut.CreateShortcut(exePath, exeArgs, finalIconPath, 0).WriteToFile(lnkPath);
        }

        // idk if these will work at all
        private static void CreateMacOsShortcut(string exePath, string exeArgs, string appBundlePath)
        {
            if (!appBundlePath.EndsWith(".app"))
                appBundlePath += ".app";

            string contentsDir = Path.Combine(appBundlePath, "Contents");
            string macOsDir = Path.Combine(contentsDir, "MacOS");

            Directory.CreateDirectory(macOsDir);

            string scriptPath = Path.Combine(macOsDir, "launcher");
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
            string finalDesktopPath = Path.ChangeExtension(desktopPath, ".desktop");
            string appName = Path.GetFileNameWithoutExtension(finalDesktopPath);
            string icon = string.IsNullOrEmpty(iconPath) ? GetFroststrapIconPath() : iconPath;

            string content =
                $"""
                [Desktop Entry]
                Type=Application
                Name={appName}
                Exec="{exePath}" {exeArgs}
                Icon={icon}
                Terminal=false
                """;

            File.WriteAllText(finalDesktopPath, content);
            Process.Start("chmod", $"+x \"{finalDesktopPath}\"")?.WaitForExit();

            // also put in a directory that app launchers index
            string appsDir = GetLinuxAppMenuDir();
            Directory.CreateDirectory(appsDir);
            string appMenuPath = Path.Combine(appsDir, Path.GetFileName(finalDesktopPath));
            File.WriteAllText(appMenuPath, content);
        }
        
        private static string GetLinuxAppMenuDir() =>
            Path.Combine(Paths.UserProfile, ".local", "share", "applications");

        private static void SaveBitmapAsIcon(Bitmap bitmap, Stream output)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms);
            ms.Position = 0;

            using var resized = Bitmap.DecodeToWidth(ms, 64);
            using var pngStream = new MemoryStream();
            resized.Save(pngStream);
            byte[] pngBytes = pngStream.ToArray();

            using var writer = new BinaryWriter(output);
            writer.Write((short)0);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write((byte)64);
            writer.Write((byte)64);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write(pngBytes.Length);
            writer.Write(22);
            writer.Write(pngBytes);
        }

        public static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        private static string ComputeHash(byte[] data)
        {
            byte[] hash = SHA256.HashData(data);
            return Convert.ToHexStringLower(hash);
        }
    }
}