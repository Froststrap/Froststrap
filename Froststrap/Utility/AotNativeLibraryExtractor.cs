// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

#if WINDOWS && FROSTSTRAP_AOT
using System.Runtime.InteropServices;

namespace Froststrap.Utility
{
    internal static class AotNativeLibraryExtractor
    {
        private const string ResourcePrefix = "Froststrap.Native.";

        private static readonly string[] NativeLibraries =
        [
            "av_libglesv2.dll",
            "libHarfBuzzSharp.dll",
            "libSkiaSharp.dll",
            "notify.dll"
        ];

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        public static void Extract()
        {
            var assembly = typeof(AotNativeLibraryExtractor).Assembly;
            string buildId = assembly.ManifestModule.ModuleVersionId.ToString("N");
            string root = Path.Combine(Path.GetTempPath(), "Froststrap", "AotNative");
            string directory = Path.Combine(root, buildId);

            Directory.CreateDirectory(root);

            foreach (string existing in Directory.GetDirectories(root))
            {
                if (string.Equals(Path.GetFileName(existing), buildId, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    Directory.Delete(existing, true);
                }
                catch
                {
                }
            }

            Directory.CreateDirectory(directory);

            bool upToDate = true;

            foreach (string name in NativeLibraries)
            {
                using Stream? resource = assembly.GetManifestResourceStream(ResourcePrefix + name);

                if (resource is null)
                    continue;

                string path = Path.Combine(directory, name);

                if (!File.Exists(path) || new FileInfo(path).Length != resource.Length)
                {
                    upToDate = false;
                    break;
                }
            }

            if (!upToDate)
            {
                foreach (string name in NativeLibraries)
                {
                    using Stream? resource = assembly.GetManifestResourceStream(ResourcePrefix + name);

                    if (resource is null)
                        continue;

                    string path = Path.Combine(directory, name);
                    string temporaryPath = path + ".tmp";

                    using (FileStream file = File.Create(temporaryPath))
                        resource.CopyTo(file);

                    File.Move(temporaryPath, path, true);
                }
            }

            foreach (string name in NativeLibraries)
            {
                string path = Path.Combine(directory, name);

                if (!File.Exists(path))
                    continue;

                try
                {
                    NativeLibrary.Load(path);
                }
                catch
                {
                }
            }

            SetDllDirectory(directory);
        }
    }
}
#endif
