// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

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
            => Paths.TryGetCustomThemeDirectory(themeName, out string? directory) && OpenFolder(directory);

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
            bool reuseWindow = kind is ExternalEditorKind.VisualStudioCode;
            bool workspaceReady = false;

            // try every install we can find: the first one that exists on disk isn't necessarily launchable
            foreach (string executable in ResolveExecutables(kind))
            {
                if (!workspaceReady)
                {
                    EnsureWorkspace(directory, kind);
                    workspaceReady = true;
                }

                if (TryStart(executable, directory, reuseWindow))
                    return true;
            }

            return false;
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
                App.Logger.Error(ex, "Failed to open directory with the system default handler");
                return false;
            }
        }

        private static IEnumerable<string> ResolveExecutables(ExternalEditorKind kind)
        {
            HashSet<string> seen = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

            foreach (string candidate in GetCandidates(kind))
            {
                string? executable = ResolveExecutable(candidate);
                if (executable != null && seen.Add(executable))
                    yield return executable;
            }
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
            if (Path.IsPathFullyQualified(candidate))
                return IsLaunchable(candidate) ? candidate : null;

            foreach (string directory in GetPathDirectories())
            {
                string fullPath = Path.Combine(directory, candidate);

                if (OperatingSystem.IsWindows() && !Path.HasExtension(candidate))
                    fullPath += ".exe";

                if (IsLaunchable(fullPath))
                    return fullPath;
            }

            return null;
        }

        private static bool IsLaunchable(string path)
        {
            if (!File.Exists(path))
                return false;

            return !OperatingSystem.IsWindows()
                || Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase);
        }

        // relative PATH entries (".", "") would resolve against whatever the working directory happens
        // to be, e.g. a Downloads folder when launched from a browser, so they're ignored
        private static IEnumerable<string> GetPathDirectories()
            => (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(Path.IsPathFullyQualified);

        private static bool TryStart(string fileName, string directory, bool reuseWindow)
        {
            try
            {
                ProcessStartInfo startInfo = new(fileName)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (reuseWindow)
                    startInfo.ArgumentList.Add("--reuse-window");

                startInfo.ArgumentList.Add(directory);

                using Process? process = Process.Start(startInfo);
                return process != null;
            }
            catch (Exception ex)
            {
                App.Logger.Warn(ex, $"Failed to launch external editor \"{fileName}\"");
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

                // installs elsewhere only put "<install>\bin\code.cmd" on PATH; launch the Code.exe next to it
                // directly instead of going through the .cmd
                foreach (string directory in GetPathDirectories())
                {
                    if (File.Exists(Path.Combine(directory, "code.cmd")))
                        yield return Path.GetFullPath(Path.Combine(directory, "..", "Code.exe"));
                }
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
    }
}
