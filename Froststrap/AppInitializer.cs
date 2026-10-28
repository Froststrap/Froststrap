// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

/// Runtime startup helpers: native library resolution for every platform and
/// macOS code-signing diagnostics.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Diagnostics;
using Froststrap;

internal static class AppInitializer
{
    [DllImport("libSystem.dylib")]
    private static extern int getpid();

    [DllImport("libSystem.dylib", SetLastError = true)]
    private static extern int csops(int pid, uint ops, out uint status, nuint size);

    [DllImport("libSystem.dylib")]
    private static extern int csr_get_active_config(out uint config);

    private const uint CS_OPS_STATUS = 0;

    private static readonly (uint Bit, string Name)[] CodeSignFlags =
    [
        (0x00000001, "VALID"),
        (0x00000002, "ADHOC"),
        (0x00000004, "GET_TASK_ALLOW"),
        (0x00000100, "HARD"),
        (0x00000200, "KILL"),
        (0x00000800, "RESTRICT"),
        (0x00001000, "ENFORCEMENT"),
        (0x00002000, "REQUIRE_LV"),
        (0x00020000, "LINKER_SIGNED"),
        (0x10000000, "RUNTIME")
    ];

    private static readonly (uint Bit, string Name)[] CsrFlags =
    [
        (1u << 0, "ALLOW_UNTRUSTED_KEXTS"),
        (1u << 1, "ALLOW_UNRESTRICTED_FS"),
        (1u << 2, "ALLOW_TASK_FOR_PID"),
        (1u << 3, "ALLOW_KERNEL_DEBUGGER"),
        (1u << 4, "ALLOW_APPLE_INTERNAL"),
        (1u << 5, "ALLOW_UNRESTRICTED_DTRACE"),
        (1u << 6, "ALLOW_UNRESTRICTED_NVRAM"),
        (1u << 10, "ALLOW_EXECUTABLE_POLICY_OVERRIDE"),
        (1u << 11, "ALLOW_UNAUTHENTICATED_ROOT")
    ];

    public static void RunMacSecurityDiagnostics()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        try
        {
            string exePath = Environment.ProcessPath ?? string.Empty;
            int appIndex = exePath.IndexOf(".app/", StringComparison.Ordinal);
            string bundlePath = appIndex >= 0 ? exePath[..(appIndex + 4)] : exePath;

            App.Logger.Debug($"Code signature: {GetSignatureSummary(bundlePath)}");
            App.Logger.Debug($"{RunTool("/usr/bin/csrutil", "status")}");
            App.Logger.Debug($"boot-args: {RunTool("/usr/sbin/nvram", "boot-args")}");

            string frameworksDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Frameworks"));
            App.Logger.Debug($"Frameworks dir: {frameworksDir} (exists: {Directory.Exists(frameworksDir)})");

            if (Directory.Exists(frameworksDir))
            {
                foreach (string dylib in Directory.EnumerateFiles(frameworksDir, "*.dylib"))
                    App.Logger.Debug($"{Path.GetFileName(dylib)}: {GetSignatureSummary(dylib)}");
            }

            try
            {
                int pid = getpid();

                if (csops(pid, CS_OPS_STATUS, out uint status, sizeof(uint)) == 0)
                {
                    string flags = string.Join(",", CodeSignFlags.Where(f => (status & f.Bit) != 0).Select(static f => f.Name));
                    App.Logger.Debug($"Process cs flags (pid {pid}): 0x{status:x} [{flags}]");
                }
                else
                {
                    App.Logger.Debug($"Process cs flags (pid {pid}): unknown (errno {Marshal.GetLastWin32Error()})");
                }

                if (csr_get_active_config(out uint csr) == 0)
                {
                    string flags = string.Join(",", CsrFlags.Where(f => (csr & f.Bit) != 0).Select(static f => f.Name));
                    App.Logger.Debug($"CSR config: 0x{csr:x} [{flags}]");
                }
                else
                {
                    App.Logger.Debug("CSR config: unknown");
                }
            }
            catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
            {
                App.Logger.Debug($"Native probes unavailable: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            App.Logger.Debug($"Diagnostics failed: {ex}");
        }
    }

    private static string GetSignatureSummary(string path)
    {
        string output = RunTool("/usr/bin/codesign", $"-dvvv \"{path}\"");

        if (output.Contains("not signed at all", StringComparison.OrdinalIgnoreCase))
            return "unsigned";

        string? signature = FindLineValue(output, "Signature=");

        if (string.Equals(signature, "adhoc", StringComparison.OrdinalIgnoreCase))
            return "adhoc";

        string authority = FindLineValue(output, "Authority=") ?? "?";
        string team = FindLineValue(output, "TeamIdentifier=") ?? "?";

        return $"{authority} (TeamIdentifier={team})";
    }

    private static string? FindLineValue(string output, string prefix)
    {
        foreach (string line in output.Split('\n'))
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return line[prefix.Length..].Trim();
        }

        return null;
    }

    private static string RunTool(string fileName, string arguments, int timeoutMs = 5000)
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return "<failed to start>";
            }

            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMs))
            {
                process.Kill();
                return "<timeout>";
            }

             process.WaitForExit();

            return $"{stdout.Result}{stderr.Result}".Trim();
        }
        catch (Exception ex)
        {
            return $"<failed: {ex.Message}>";
        }
    }

    private static readonly string[] ResolverAssemblies =
    [
        "Froststrap",
        "backend",
        "SkiaSharp",
        "HarfBuzzSharp",
        "Avalonia.Native",
        "Avalonia.Win32"
    ];

    private static string[] _nativeLibraryDirectories = [];
    private static bool _resolversInitialized;

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    public static void InitializeNativeResolvers()
    {
        if (_resolversInitialized)
            return;

        _resolversInitialized = true;
        _nativeLibraryDirectories = GetNativeLibraryDirectories();

        if (OperatingSystem.IsWindows())
        {
            string? libraryDirectory = _nativeLibraryDirectories.FirstOrDefault(Directory.Exists);

            if (libraryDirectory is not null)
                SetDllDirectory(libraryDirectory);
        }

        AppDomain.CurrentDomain.AssemblyLoad += static (_, args) => RegisterResolver(args.LoadedAssembly);

        HashSet<Assembly> assemblies =
        [
            typeof(App).Assembly,
            typeof(Froststrap.Backend.VirtualDisplay).Assembly,
            typeof(SkiaSharp.SKCanvas).Assembly,
            typeof(HarfBuzzSharp.Blob).Assembly
        ];

        assemblies.UnionWith(AppDomain.CurrentDomain.GetAssemblies());

        foreach (Assembly assembly in assemblies)
            RegisterResolver(assembly);
    }

    private static string[] GetNativeLibraryDirectories()
    {
        string baseDirectory = AppContext.BaseDirectory;

        if (OperatingSystem.IsMacOS())
        {
            return
            [
                Path.GetFullPath(Path.Combine(baseDirectory, "..", "Frameworks")),
                Path.Combine(baseDirectory, "libs"),
                baseDirectory
            ];
        }

        if (OperatingSystem.IsLinux())
        {
            return
            [
                Path.Combine(baseDirectory, "libs"),
                "/usr/lib/froststrap",
                baseDirectory
            ];
        }

        return
        [
            Path.Combine(baseDirectory, "libs"),
            baseDirectory
        ];
    }

    private static void RegisterResolver(Assembly assembly)
    {
        string? assemblyName = assembly.GetName().Name;

        if (assemblyName is null || !ResolverAssemblies.Contains(assemblyName, StringComparer.Ordinal))
            return;

        try
        {
            NativeLibrary.SetDllImportResolver(assembly, ResolveNativeLibrary);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static IntPtr ResolveNativeLibrary(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        foreach (string fileName in GetCandidateFileNames(libraryName))
        {
            foreach (string directory in _nativeLibraryDirectories)
            {
                string fullPath = Path.Combine(directory, fileName);

                if (File.Exists(fullPath) && NativeLibrary.TryLoad(fullPath, out IntPtr handle))
                    return handle;
            }
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> GetCandidateFileNames(string libraryName)
    {
        if (Path.HasExtension(libraryName))
        {
            yield return libraryName;
            yield break;
        }

        string extension = OperatingSystem.IsWindows()
            ? ".dll"
            : OperatingSystem.IsMacOS()
                ? ".dylib"
                : ".so";

        yield return libraryName + extension;

        if (!libraryName.StartsWith("lib", StringComparison.OrdinalIgnoreCase))
            yield return "lib" + libraryName + extension;
    }
}
