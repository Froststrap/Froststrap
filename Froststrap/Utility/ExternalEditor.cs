// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel;

namespace Froststrap.Utility
{
    internal static class ExternalEditor
    {
        private static readonly ExternalEditorKind[] AutoOrder =
        [
            ExternalEditorKind.Zed,
            ExternalEditorKind.VisualStudioCode,
            ExternalEditorKind.SublimeText
        ];

        public static bool OpenTheme(string themeName)
            => OpenFolder(Path.Combine(Paths.CustomThemes, themeName));

        public static bool OpenFolder(string directory)
        {
            if (!Directory.Exists(directory))
                return false;

            ExternalEditorKind preference = App.Settings.Prop.ExternalEditor;

            if (preference == ExternalEditorKind.SystemDefault)
                return OpenWithSystemDefault(directory);

            if (preference != ExternalEditorKind.Auto)
            {
                if (TryOpen(directory, preference))
                    return true;

                return OpenWithSystemDefault(directory);
            }

            foreach (ExternalEditorKind kind in AutoOrder)
            {
                if (TryOpen(directory, kind))
                    return true;
            }

            return OpenWithSystemDefault(directory);
        }

        private static bool TryOpen(string directory, ExternalEditorKind kind)
        {
            EditorLauncher? launcher = ResolveLauncher(kind);
            if (launcher == null)
                return false;

            EnsureWorkspace(directory, kind);

            return TryStart(launcher.FileName, directory, launcher.ReuseWindow);
        }

        private static void EnsureWorkspace(string directory, ExternalEditorKind kind)
        {
            if (kind is ExternalEditorKind.VisualStudioCode)
                ThemeEditorWorkspace.EnsureVisualStudioCode(directory);
        }

        private static bool OpenWithSystemDefault(string directory)
        {
            try
            {
                Threading.ShellExecute(directory);
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to open directory with the system default handler", ex);
                return false;
            }
        }

        private static EditorLauncher? ResolveLauncher(ExternalEditorKind kind)
        {
            bool reuseWindow = kind is ExternalEditorKind.VisualStudioCode;

            foreach (string candidate in GetCandidates(kind))
            {
                string? executable = ResolveExecutable(candidate);
                if (executable != null)
                    return new EditorLauncher(executable, reuseWindow);
            }

            return null;
        }

        private static IEnumerable<string> GetCandidates(ExternalEditorKind kind) => kind switch
        {
            ExternalEditorKind.Zed => GetZedCandidates(),
            ExternalEditorKind.VisualStudioCode => GetVisualStudioCodeCandidates(),
            ExternalEditorKind.SublimeText => GetSublimeTextCandidates(),
            _ => []
        };

        private static string? ResolveExecutable(string candidate)
        {
            if (Path.IsPathRooted(candidate)
                || candidate.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || candidate.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
            {
                return File.Exists(candidate) ? candidate : null;
            }

            string? path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
                return null;

            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string fullPath = Path.Combine(directory, candidate);

                if (File.Exists(fullPath))
                    return fullPath;

                if (OperatingSystem.IsWindows())
                {
                    foreach (string extension in new[] { ".exe", ".cmd", ".bat" })
                    {
                        string withExtension = fullPath + extension;

                        if (File.Exists(withExtension))
                            return withExtension;
                    }
                }
            }

            return null;
        }

        private static bool TryStart(string fileName, string directory, bool reuseWindow)
        {
            try
            {
                ProcessStartInfo startInfo;

                if (OperatingSystem.IsWindows() && fileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
                {
                    startInfo = new ProcessStartInfo("cmd.exe")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    startInfo.ArgumentList.Add("/c");
                    startInfo.ArgumentList.Add(fileName);
                }
                else
                {
                    startInfo = new ProcessStartInfo(fileName)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                }

                if (reuseWindow)
                    startInfo.ArgumentList.Add("--reuse-window");

                startInfo.ArgumentList.Add(directory);

                using Process? process = Process.Start(startInfo);
                return process != null;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                return false;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to launch external editor \"{fileName}\"", ex);
                return false;
            }
        }

        private static IEnumerable<string> GetZedCandidates()
        {
            yield return "zed";
            yield return "zeditor";

            if (OperatingSystem.IsWindows())
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

                yield return Path.Combine(localAppData, "Programs", "Zed", "zed.exe");
                yield return Path.Combine(localAppData, "Zed", "zed.exe");
                yield return Path.Combine(programFiles, "Zed", "zed.exe");
            }
            else if (OperatingSystem.IsMacOS())
            {
                yield return "/Applications/Zed.app/Contents/MacOS/cli";
                yield return Path.Combine(Paths.UserProfile, "Applications", "Zed.app", "Contents", "MacOS", "cli");
                yield return "/usr/local/bin/zed";
            }
        }

        private static IEnumerable<string> GetVisualStudioCodeCandidates()
        {
            yield return "code";

            if (OperatingSystem.IsWindows())
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                yield return Path.Combine(localAppData, "Programs", "Microsoft VS Code", "Code.exe");
                yield return Path.Combine(programFiles, "Microsoft VS Code", "Code.exe");
                yield return Path.Combine(programFilesX86, "Microsoft VS Code", "Code.exe");
                yield return "code.cmd";
            }
            else if (OperatingSystem.IsMacOS())
            {
                yield return "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code";
                yield return Path.Combine(Paths.UserProfile, "Applications", "Visual Studio Code.app", "Contents", "Resources", "app", "bin", "code");
            }
        }

        private static IEnumerable<string> GetSublimeTextCandidates()
        {
            yield return "subl";

            if (OperatingSystem.IsWindows())
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

                yield return Path.Combine(programFiles, "Sublime Text", "subl.exe");
                yield return Path.Combine(programFiles, "Sublime Text 3", "subl.exe");
            }
            else if (OperatingSystem.IsMacOS())
            {
                yield return "/Applications/Sublime Text.app/Contents/SharedSupport/bin/subl";
            }
        }

        private sealed record EditorLauncher(string FileName, bool ReuseWindow);
    }
}
