using System;
using System.Runtime.InteropServices;
using Fallout.Common;

public partial class Build : FalloutBuild
{
    string TargetArch => !string.IsNullOrWhiteSpace(OverrideArch)
        ? NormalizeArch(OverrideArch)
        : RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported host architecture '{RuntimeInformation.OSArchitecture}'.")
        };

    string RustTargetTriple => TargetArch switch
    {
        "x64" when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => "x86_64-apple-darwin",
        "arm64" when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => "aarch64-apple-darwin",
        "x64" when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "x86_64-unknown-linux-gnu",
        "arm64" when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "aarch64-unknown-linux-gnu",
        "x64" when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => "x86_64-pc-windows-msvc",
        "arm64" when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => "aarch64-pc-windows-msvc",
        _ => throw new PlatformNotSupportedException(
            $"Unsupported target architecture '{TargetArch}' for the current operating system.")
    };

    static string NormalizeArch(string arch) => arch.Trim().ToLowerInvariant() switch
    {
        "x64" or "x86_64" or "x86-64" or "amd64" => "x64",
        "arm64" or "aarch64" => "arm64",
        _ => throw new ArgumentException($"Unsupported architecture '{arch}'. Expected x64 or arm64.")
    };
}
