// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Win32;

namespace Froststrap
{
    internal class Installer
    {
        // TODO: Update all shortcuts to new directory
        public static async Task MoveInstallation(string newDir)
        {
            string oldDir = Paths.Base;
            if (string.Equals(oldDir, newDir, StringComparison.OrdinalIgnoreCase))
                return;

            Directory.CreateDirectory(newDir);
            string testFile = Path.Combine(newDir, ".writetest");
            try
            {
                await File.WriteAllTextAsync(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                throw new IOException($"Cannot write to new directory: {ex.Message}", ex);
            }

            string[] itemsToMove = [
                "Settings.json",
                "State.json",
                "StudioState.json",
                "PlayerState.json",
                "Uninstall.exe",
                "Cache",
                "CustomCursorsSets",
                "CustomThemes",
                "Modifications"
            ];

            string exeName = Path.GetFileName(Paths.Process);

            foreach (string item in itemsToMove)
            {
                string source = Path.Combine(oldDir, item);
                string dest = Path.Combine(newDir, item);

                if (!File.Exists(source) && !Directory.Exists(source))
                {
                    App.Logger.Info($"Skipping missing item: {item}");
                    continue;
                }

                try
                {
                    if (Directory.Exists(source))
                    {
                        Directory.CreateDirectory(dest);
                        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                        {
                            string relDir = Path.GetRelativePath(source, dir);
                            Directory.CreateDirectory(Path.Combine(dest, relDir));
                        }
                        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                        {
                            string relFile = Path.GetRelativePath(source, file);
                            string targetFile = Path.Combine(dest, relFile);
                            File.Copy(file, targetFile, true);
                        }
                    }
                    else if (File.Exists(source))
                    {
                        File.Copy(source, dest, true);
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex);
                    throw new IOException($"Failed to copy {item}: {ex.Message}", ex);
                }
            }

            string oldExePath = Paths.Process;
            string newExePath = Path.Combine(newDir, exeName);

            bool copied = false;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    using (var srcStream = new FileStream(oldExePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var dstStream = new FileStream(newExePath, FileMode.Create, FileAccess.Write))
                    {
                        await srcStream.CopyToAsync(dstStream);
                    }

                    var srcInfo = new FileInfo(oldExePath);
                    var dstInfo = new FileInfo(newExePath);
                    if (srcInfo.Length != dstInfo.Length)
                        throw new IOException("Size mismatch after copy.");

                    string srcHash = FastHash.FromFile(oldExePath);
                    string dstHash = FastHash.FromFile(newExePath);
                    if (srcHash != dstHash)
                        throw new IOException("Hash mismatch after copy.");

                    copied = true;
                    break;
                }
                catch (Exception ex)
                {
                    App.Logger.Info($"Copy attempt {i + 1} failed: {ex.Message}");
                    if (i < 4) await Task.Delay(500);
                }
            }
            if (!copied)
                throw new IOException("Failed to copy executable after multiple retries.");

            UpdateRegistryForNewLocation(newDir);

            string batchName = "MoveHelper.bat";
            string batchPath = Path.Combine(newDir, batchName);

            string batchContent = $@"@echo off
timeout /t 3 /nobreak >nul
start """" ""{newExePath}"" -settings
timeout /t 3 /nobreak >nul
rmdir /s /q ""{oldDir}""
del ""%~f0""
";
            await File.WriteAllTextAsync(batchPath, batchContent);

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            Environment.Exit(0);
        }

        private static void UpdateRegistryForNewLocation(string newDir)
        {
            if (!OperatingSystem.IsWindows()) return;

            string exePath = Path.Combine(newDir, Path.GetFileName(Paths.Process));

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Froststrap");
            key.SetValue("InstallLocation", newDir);
            key.SetValue("AppPath", exePath);

            using var uninstallKey = Registry.CurrentUser.CreateSubKey(App.UninstallKey);
            uninstallKey.SetValue("InstallLocation", newDir);
            uninstallKey.SetValue("UninstallString", $"\"{Path.Combine(newDir, "Uninstall.exe")}\"");
            uninstallKey.SetValue("QuietUninstallString", $"\"{Path.Combine(newDir, "Uninstall.exe")}\" /S");
            uninstallKey.SetValue("DisplayIcon", $"{exePath},0");
            uninstallKey.SetValue("ModifyPath", $"\"{exePath}\" -settings");

            using var appPaths = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{Path.GetFileName(exePath)}");
            appPaths.SetValue("", exePath);
        }
    }
}
