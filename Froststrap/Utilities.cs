using Froststrap.AppData;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Froststrap
{
    static partial class Utilities
    {
        public static void ShellExecute(string path, bool select = false)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = select ? "explorer.exe" : path,
                        UseShellExecute = true
                    };

                    if (select)
                    {
                        psi.ArgumentList.Add($"/select,\"{path}\"");
                    }

                    Process.Start(psi);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    string target = select ? (Path.GetDirectoryName(path) ?? path) : path;

                    var psi = new ProcessStartInfo("xdg-open")
                    {
                        UseShellExecute = false
                    };
                    psi.ArgumentList.Add(target);

                    Process.Start(psi);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var psi = new ProcessStartInfo("open")
                    {
                        UseShellExecute = false
                    };

                    if (select)
                    {
                        psi.ArgumentList.Add("-R");
                    }
                    psi.ArgumentList.Add(path);

                    Process.Start(psi);
                }
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode != (int)ErrorCode.CO_E_APPNOTFOUND)
                    throw;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "rundll32.exe",
                        Arguments = $"shell32,OpenAs_RunDLL {path}",
                        UseShellExecute = true
                    });
                }
            }
        }

        public static Version GetVersionFromString(string version)
        {
            if (version.StartsWith('v'))
                version = version[1..];

            int idx = version.IndexOf('+', StringComparison.Ordinal);
            if (idx != -1)
                version = version[..idx];

            int dashIdx = version.IndexOf('-', StringComparison.Ordinal);
            if (dashIdx != -1)
                version = version[..dashIdx];

            return new Version(version);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="versionStr1"></param>
        /// <param name="versionStr2"></param>
        /// <returns>
        /// Result of System.Version.CompareTo <br />
        /// -1: version1 &lt; version2 <br />
        ///  0: version1 == version2 <br />
        ///  1: version1 &gt; version2
        /// </returns>
        public static VersionComparison CompareVersions(string versionStr1, string versionStr2)
        {
            try
            {
                var (version1, prerelease1) = GetVersionParts(versionStr1);
                var (version2, prerelease2) = GetVersionParts(versionStr2);

                var versionComparison = (VersionComparison)version1.CompareTo(version2);

                if (versionComparison != VersionComparison.Equal)
                    return versionComparison;

                return ComparePrerelease(prerelease1, prerelease2);
            }
            catch (Exception ex)
            {
                // temporary diagnostic log for the issue described here:
                // https://github.com/Bloxstraplabs/Bloxstrap/issues/3193
                // the problem is that this happens only on upgrade, so my only hope of catching this is bug reports following the next release

                App.Logger.Info($"versionStr1={versionStr1} versionStr2={versionStr2}");
                App.Logger.Error($"An exception occurred when comparing versions: {ex}");

                throw;
            }
        }

        private static (Version Version, string? Prerelease) GetVersionParts(string version)
        {
            if (version.StartsWith('v'))
                version = version[1..];

            int idx = version.IndexOf('+', StringComparison.Ordinal);
            if (idx != -1)
                version = version[..idx];

            string? prerelease = null;
            int dashIdx = version.IndexOf('-', StringComparison.Ordinal);
            if (dashIdx != -1)
            {
                prerelease = version[(dashIdx + 1)..];
                version = version[..dashIdx];
            }

            return (new Version(version), prerelease);
        }

        private static VersionComparison ComparePrerelease(string? prerelease1, string? prerelease2)
        {
            if (string.IsNullOrEmpty(prerelease1) && string.IsNullOrEmpty(prerelease2))
                return VersionComparison.Equal;

            if (string.IsNullOrEmpty(prerelease1))
                return VersionComparison.GreaterThan;

            if (string.IsNullOrEmpty(prerelease2))
                return VersionComparison.LessThan;

            string[] parts1 = prerelease1.Split('.');
            string[] parts2 = prerelease2.Split('.');

            for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
            {
                if (i >= parts1.Length)
                    return VersionComparison.LessThan;

                if (i >= parts2.Length)
                    return VersionComparison.GreaterThan;

                string part1 = parts1[i];
                string part2 = parts2[i];

                bool part1IsNumber = int.TryParse(part1, NumberStyles.None, CultureInfo.InvariantCulture, out int part1Number);
                bool part2IsNumber = int.TryParse(part2, NumberStyles.None, CultureInfo.InvariantCulture, out int part2Number);

                if (part1IsNumber && part2IsNumber)
                {
                    int numberComparison = part1Number.CompareTo(part2Number);
                    if (numberComparison != 0)
                        return (VersionComparison)numberComparison;
                }
                else if (part1IsNumber != part2IsNumber)
                {
                    return part1IsNumber ? VersionComparison.LessThan : VersionComparison.GreaterThan;
                }
                else
                {
                    int stringComparison = string.CompareOrdinal(part1, part2);
                    if (stringComparison != 0)
                        return stringComparison < 0 ? VersionComparison.LessThan : VersionComparison.GreaterThan;
                }
            }

            return VersionComparison.Equal;
        }

        /// <summary>
        /// Parses the input version string and prints if fails
        /// </summary>
        public static Version? ParseVersionSafe(string versionStr)
        {
            if (!Version.TryParse(versionStr, out Version? version))
            {
                App.Logger.Error($"Failed to convert {versionStr} to a valid Version type.");
                return version;
            }

            return version;
        }

        public static string GetRobloxVersionStr(IAppData data)
        {
            string playerLocation = data.ExecutablePath;

            if (!File.Exists(playerLocation))
                return "";

            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(playerLocation);

            if (versionInfo.ProductVersion is null)
                return "";

            return versionInfo.ProductVersion.Replace(", ", ".", StringComparison.Ordinal);
        }

        public static string GetRobloxVersionStr(bool studio)
        {
            IAppData data = studio ? new RobloxStudioData() : new RobloxPlayerData();

            return GetRobloxVersionStr(data);
        }

        public static Version? GetRobloxVersion(IAppData data)
        {
            string str = GetRobloxVersionStr(data);
            return ParseVersionSafe(str);
        }

        public static Process[] GetProcessesSafe()
        {
            try
            {
                return Process.GetProcesses();
            }
            catch (ArithmeticException ex) // thanks microsoft
            {
                App.Logger.Error($"Unable to fetch processes! {ex}");
                return []; // can we retry?
            }
        }

        public static bool IsRobloxRunning()
        {
            Process[] processes = GetProcessesSafe();
            string processName = Path.GetFileNameWithoutExtension(App.RobloxPlayerAppName);

            if (OperatingSystem.IsLinux())
                return processes.Any(x => x.ProcessName == "sober");
            else
                return processes.Any(x => x.ProcessName == processName);
        }

        public static void KillSober()
        {
            Process[] processes = GetProcessesSafe();
            foreach (var p in processes)
            {
                if (p.ProcessName == "sober")
                {
                    try { p.Kill(); p.WaitForExit(1000); } catch { }
                }
            }
        }

        public static void KillBackgroundUpdater()
        {
            using EventWaitHandle handle = new(false, EventResetMode.AutoReset, "Froststrap-BackgroundUpdaterKillEvent");
            handle.Set();
        }
    }
}
