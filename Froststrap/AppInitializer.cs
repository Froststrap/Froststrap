// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

/// macOS related runtime startup, debug related stuff will mostly be logging stuff
/// related for settings, which might if not known will make stuff not repro.

using System.Reflection;
using System.Runtime.InteropServices;
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

    public static void InitializeNativeResolvers()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        List<Assembly> assemblies =
        [
            typeof(Froststrap.Backend.VirtualDisplay).Assembly,
            typeof(SkiaSharp.SKCanvas).Assembly,
            typeof(HarfBuzzSharp.Blob).Assembly
        ];

        Assembly? avaloniaNative = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(static a => a.GetName().Name == "Avalonia.Native");

        if (avaloniaNative is not null)
            assemblies.Add(avaloniaNative);

        foreach (Assembly assembly in assemblies)
        {
            try
            {
                NativeLibrary.SetDllImportResolver(assembly, ResolveBundleFramework);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private static IntPtr ResolveBundleFramework(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        string baseDir = AppContext.BaseDirectory;

        List<string> fileNames = [];

        if (libraryName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase))
        {
            fileNames.Add(libraryName);
        }
        else
        {
            fileNames.Add($"{libraryName}.dylib");

            if (!libraryName.StartsWith("lib", StringComparison.Ordinal))
                fileNames.Add($"lib{libraryName}.dylib");
        }

        string[] directories =
        [
            Path.Combine(baseDir, "..", "Frameworks"),
            baseDir
        ];

        foreach (string directory in directories)
        {
            foreach (string fileName in fileNames)
            {
                string fullPath = Path.GetFullPath(Path.Combine(directory, fileName));

                if (File.Exists(fullPath) && NativeLibrary.TryLoad(fullPath, out IntPtr handle))
                    return handle;
            }
        }

        return IntPtr.Zero;
    }
}
