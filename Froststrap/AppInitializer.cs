// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Reflection;
using System.Runtime.InteropServices;
using Froststrap;

internal static class AppInitializer
{
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
