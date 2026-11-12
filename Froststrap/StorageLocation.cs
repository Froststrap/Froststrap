// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;

namespace Froststrap
{
    // Relocates bulky data folders by moving them and leaving a link (junction on Windows,
    // symlink elsewhere) at the common path, so the rest of the app keeps using Paths.* unchanged.
    internal static class StorageLocation
    {
        private static string[] Folders => [Paths.Versions, Paths.Downloads];

        public static string Current
        {
            get
            {
                var target = new DirectoryInfo(Paths.Versions).ResolveLinkTarget(true)?.FullName;
                return target is null ? Paths.DataRoot : Directory.GetParent(target)!.FullName;
            }
        }

        public static void Move(string newRoot)
        {
            newRoot = Path.GetFullPath(newRoot);
            Directory.CreateDirectory(newRoot);

            foreach (string link in Folders)
            {
                string dest = Path.Combine(newRoot, Path.GetFileName(link));
                var info = new DirectoryInfo(link);
                string? real = info.ResolveLinkTarget(true)?.FullName;

                if (string.Equals(real ?? link, dest, StringComparison.Ordinal))
                    continue;

                if (Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any())
                    throw new IOException($"'{dest}' already contains data.");

                if (real is not null)
                {
                    info.Delete();
                    MoveDirectory(real, dest);
                }
                else if (info.Exists)
                {
                    MoveDirectory(link, dest);
                }
                else
                {
                    Directory.CreateDirectory(dest);
                }

                if (!string.Equals(dest, link, StringComparison.Ordinal))
                    CreateLink(link, dest);
            }
        }

        private static void MoveDirectory(string source, string dest)
        {
            if (Directory.Exists(dest))
                Directory.Delete(dest);

            try
            {
                Directory.Move(source, dest);
            }
            catch (IOException) when (Directory.Exists(source))
            {
                CopyDirectory(source, dest);
                Directory.Delete(source, true);
            }
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);

            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);

            foreach (string dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }

        internal static void CreateLink(string link, string target)
        {
            if (!OperatingSystem.IsWindows())
            {
                Directory.CreateSymbolicLink(link, target);
                return;
            }

            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            })!;
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new IOException($"Failed to create junction '{link}'.");
        }
    }
}
