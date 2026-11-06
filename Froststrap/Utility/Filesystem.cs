// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Froststrap.Utility
{
    internal static class Filesystem
    {
        internal static long GetFreeDiskSpace(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return -1;

            string fullPath;

            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Could not resolve path '{path}' for the disk space check: {ex.Message}");
                return -1;
            }

            if (OperatingSystem.IsWindows())
                return TryGetWindowsFreeSpace(fullPath, out long windowsFreeSpace) ? windowsFreeSpace : -1;

            try
            {
                return new DriveInfo(fullPath).AvailableFreeSpace;
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Could not read free disk space for '{fullPath}': {ex.Message}");
                return -1;
            }
        }

        [SupportedOSPlatform("windows")]
        private static bool TryGetWindowsFreeSpace(string path, out long freeSpace)
        {
            freeSpace = -1;

            try
            {
                if (!GetDiskFreeSpaceEx(path, out ulong availableFreeSpace, out _, out _))
                    return false;

                freeSpace = availableFreeSpace > long.MaxValue ? long.MaxValue : (long)availableFreeSpace;
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Could not read free disk space for '{path}': {ex.Message}");
                return false;
            }
        }

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpaceEx(
            string lpDirectoryName,
            out ulong lpFreeBytesAvailableToCaller,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);

        internal static void AssertReadOnly(string filePath)
        {
            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists || !fileInfo.IsReadOnly)
                return;

            fileInfo.IsReadOnly = false;
            App.Logger.Info($"The following file was set as read-only: {filePath}");
        }

        internal static void AssertReadOnlyDirectory(string directoryPath)
        {
            var directory = new DirectoryInfo(directoryPath) { Attributes = FileAttributes.Normal };

            foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
                info.Attributes = FileAttributes.Normal;

            App.Logger.Info($"The following directory was set as read-only: {directoryPath}");
        }
    }
}
