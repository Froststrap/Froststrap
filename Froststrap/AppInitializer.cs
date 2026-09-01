using System.Reflection;
using System.Runtime.InteropServices;
using Froststrap;

internal static class AppInitializer
{
    public static void InitializeNativeResolvers()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            App.Logger.Debug("Initializing Dll Native Resolvers");
            NativeLibrary.SetDllImportResolver(
                Assembly.GetExecutingAssembly(), 
                ResolveBundleFramework
            );
        }
    }

    private static IntPtr ResolveBundleFramework(
        string libraryName, 
        Assembly assembly, 
        DllImportSearchPath? searchPath)
    {
        string baseDir = AppContext.BaseDirectory;

        string fileName = libraryName;
        if (!fileName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase))
        {
            fileName = $"lib{libraryName}.dylib";
        }

        string[] candidatePaths =
        [
            // Same folder as executable
            Path.Combine(baseDir, fileName),
            Path.Combine(baseDir, libraryName),

            // Bundle Frameworks
            Path.Combine(baseDir, "..", "Frameworks", fileName),
            Path.Combine(baseDir, "..", "Frameworks", libraryName)
        ];

        foreach (string relativePath in candidatePaths)
        {
            string fullPath = Path.GetFullPath(relativePath);
            
            if (File.Exists(fullPath) && NativeLibrary.TryLoad(fullPath, out IntPtr handle)) return handle;
        }

        return IntPtr.Zero;
    }
}
