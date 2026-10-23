// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

#if _WIN32 && __linux__ && FROSTSTRAP_AOT
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Froststrap.Utility
{
    internal static class AotNativeLibraryExtractor
    {
        private const string ResourcePrefix = "Froststrap.Native.";
        private const string ManifestResource = ResourcePrefix + "manifest";
        private const string ReadyFileName = ".ready";

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
            Assembly assembly = typeof(AotNativeLibraryExtractor).Assembly;
            var expected = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string name in NativeLibraries)
            {
                using Stream? resource = assembly.GetManifestResourceStream(ResourcePrefix + name);

                if (resource is not null)
                    expected[name] = resource.Length;
            }

            ReadManifest(assembly, hashes);

            string baseDirectory = AppContext.BaseDirectory;

            if (expected.Count == 0)
            {
                if (AllPresent(baseDirectory))
                    return;

                throw new FileNotFoundException(
                    "Froststrap is missing required components. Please reinstall Froststrap.");
            }

            if (Matches(baseDirectory, expected))
                return;

            RemoveStaleLocalLibraries(baseDirectory, expected);

            string directoryName = hashes.Count > 0
                ? HashManifest(hashes)
                : assembly.ManifestModule.ModuleVersionId.ToString("N");
            string root = Path.Combine(Path.GetTempPath(), "Froststrap", "AotNative");
            string directory = Path.Combine(root, directoryName);
            string marker = Path.Combine(directory, ReadyFileName);

            if (!File.Exists(marker) || !Matches(directory, expected))
                ExtractLibraries(directory, expected, hashes, assembly, marker);

            _ = Task.Run(() => CleanupStaleDirectories(root, directoryName));

            SetDllDirectory(directory);
        }

        private static void ReadManifest(Assembly assembly, Dictionary<string, string> hashes)
        {
            using Stream? stream = assembly.GetManifestResourceStream(ManifestResource);

            if (stream is null)
                return;

            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line;

            while ((line = reader.ReadLine()) is not null)
            {
                int separator = line.LastIndexOf('|');

                if (separator <= 0 || separator == line.Length - 1)
                    continue;

                hashes[line[..separator]] = line[(separator + 1)..];
            }
        }

        private static string HashManifest(Dictionary<string, string> hashes)
        {
            var builder = new StringBuilder();

            foreach (string name in hashes.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
                builder.Append(name).Append('|').Append(hashes[name]).Append('\n');

            byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));

            return Convert.ToHexString(digest)[..16];
        }

        private static bool Matches(string directory, Dictionary<string, long> expected)
        {
            foreach (var (name, length) in expected)
            {
                var file = new FileInfo(Path.Combine(directory, name));

                if (!file.Exists || file.Length != length)
                    return false;
            }

            return true;
        }

        private static bool AllPresent(string directory)
        {
            foreach (string name in NativeLibraries)
            {
                if (!File.Exists(Path.Combine(directory, name)))
                    return false;
            }

            return true;
        }

        private static void RemoveStaleLocalLibraries(string directory, Dictionary<string, long> expected)
        {
            foreach (var (name, length) in expected)
            {
                string path = Path.Combine(directory, name);

                try
                {
                    var file = new FileInfo(path);

                    if (file.Exists && file.Length != length)
                        file.Delete();
                }
                catch
                {
                }
            }
        }

        private static void ExtractLibraries(string directory, Dictionary<string, long> expected,
            Dictionary<string, string> hashes, Assembly assembly, string marker)
        {
            Directory.CreateDirectory(directory);
            WriteLibraries(directory, expected, assembly, false);

            try
            {
                VerifyHashes(directory, hashes);
            }
            catch (InvalidDataException)
            {
                WriteLibraries(directory, expected, assembly, true);
                VerifyHashes(directory, hashes);
            }

            File.WriteAllText(marker, string.Empty);
        }

        private static void WriteLibraries(string directory, Dictionary<string, long> expected,
            Assembly assembly, bool force)
        {
            Directory.CreateDirectory(directory);

            foreach (var (name, length) in expected)
            {
                string path = Path.Combine(directory, name);

                if (!force && File.Exists(path) && new FileInfo(path).Length == length)
                    continue;

                using Stream? resource = assembly.GetManifestResourceStream(ResourcePrefix + name);

                if (resource is null)
                    throw new FileNotFoundException("Froststrap installation is incomplete. Please reinstall Froststrap.");

                string temporaryPath = path + ".tmp";

                using (FileStream file = File.Create(temporaryPath))
                    resource.CopyTo(file);

                File.Move(temporaryPath, path, true);
            }
        }

        private static void VerifyHashes(string directory, Dictionary<string, string> hashes)
        {
            foreach (var (name, hash) in hashes)
            {
                string path = Path.Combine(directory, name);

                using FileStream stream = File.OpenRead(path);
                string actual = Convert.ToHexString(SHA256.HashData(stream));

                if (!string.Equals(actual, hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Native library {name} failed hash validation.");
            }
        }

        private static void CleanupStaleDirectories(string root, string currentDirectory)
        {
            try
            {
                foreach (string existing in Directory.GetDirectories(root))
                {
                    if (string.Equals(Path.GetFileName(existing), currentDirectory, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        Directory.Delete(existing, true);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}
#endif
