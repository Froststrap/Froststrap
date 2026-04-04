using Froststrap;
using Froststrap.AppData;
using System.ComponentModel;
using System.Globalization;

namespace Froststrap
{
    static class Utilities
    {
        public static void ShellExecute(string website)
        {
            try
            {
                Process.Start(new ProcessStartInfo 
                { 
                    FileName = website, 
                    UseShellExecute = true 
                });
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode != (int)ErrorCode.CO_E_APPNOTFOUND)
                    throw;

                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"shell32,OpenAs_RunDLL {website}"
                });
            }
        }

        public static Version GetVersionFromString(string version)
        {
            if (version.StartsWith('v'))
                version = version[1..];

            int idx = version.IndexOf('+'); // commit info
            if (idx != -1)
                version = version[..idx];

            int dashIdx = version.IndexOf('-');
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
            catch (Exception)
            {
                // temporary diagnostic log for the issue described here:
                // https://github.com/Bloxstraplabs/Bloxstrap/issues/3193
                // the problem is that this happens only on upgrade, so my only hope of catching this is bug reports following the next release

                App.Logger.WriteLine("Utilities::CompareVersions", "An exception occurred when comparing versions");
                App.Logger.WriteLine("Utilities::CompareVersions", $"versionStr1={versionStr1} versionStr2={versionStr2}");

                throw;
            }
        }

        private static (Version Version, string? Prerelease) GetVersionParts(string version)
        {
            if (version.StartsWith('v'))
                version = version[1..];

            int idx = version.IndexOf('+');
            if (idx != -1)
                version = version[..idx];

            string? prerelease = null;
            int dashIdx = version.IndexOf('-');
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
            const string LOG_IDENT = "Utilities::ParseVersionSafe";

            if (!Version.TryParse(versionStr, out Version? version))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to convert {versionStr} to a valid Version type.");
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

            return versionInfo.ProductVersion.Replace(", ", ".");
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
            const string LOG_IDENT = "Utilities::GetProcessesSafe";

            try
            {
                return Process.GetProcesses();
            }
            catch (ArithmeticException ex) // thanks microsoft
            {
                App.Logger.WriteLine(LOG_IDENT, $"Unable to fetch processes!");
                App.Logger.WriteException(LOG_IDENT, ex);
                return Array.Empty<Process>(); // can we retry?
            }
        }

        public static bool DoesMutexExist(string name)
        {
            try
            {
                Mutex.OpenExisting(name).Close();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool DoesEventExist(string name)
        {
#if WINDOWS
            try
            {
                using (EventWaitHandle.OpenExisting(name)) { }
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
#else
            // Not supported on operating systems other than windows
            return false;
#endif
        }


        public static bool IsRobloxRunning()
        {
            Process[] processes = GetProcessesSafe();
            string processName = Path.GetFileNameWithoutExtension(App.RobloxPlayerAppName);

            return processes.Any(x => x.ProcessName == processName);
        }

        public static void KillBackgroundUpdater()
        {
            using EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.AutoReset, "Froststrap-BackgroundUpdaterKillEvent");
            handle.Set();
        }
    }
}
