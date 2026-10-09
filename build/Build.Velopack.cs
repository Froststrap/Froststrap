using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;

public partial class Build : FalloutBuild
{
    AbsolutePath VelopackDir => OutputRoot / "velopack";

    static string Vpk()
    {
        string local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dotnet", "tools", OperatingSystem.IsWindows() ? "vpk.exe" : "vpk");
        return File.Exists(local) ? local : "vpk";
    }

    void PackVelopack(AbsolutePath packDir = null)
    {
        packDir ??= DotnetPublishArtifactsDir;
        string version = GitTag.TrimStart('v');
        string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
                  : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
                  : "linux";

        string channel = (os, TargetArch) switch
        {
            ("windows", "x64") => "windows-x64",
            ("macos", "x64" or "arm64") => $"macos-{TargetArch}",
            ("linux", "x64") => "linux-x64",
            _ => throw new PlatformNotSupportedException(
                $"Velopack does not support channel for {RuntimeInformation.OSDescription} {TargetArch}.")
        };

        Directory.CreateDirectory(VelopackDir);

        var args = new StringBuilder();
        args.Append($"pack --packId Froststrap --packTitle Froststrap --packAuthors Froststrap ");
        args.Append($"--packVersion \"{version}\" --packDir \"{packDir}\" --outputDir \"{VelopackDir}\" ");
        args.Append($"--mainExe {(os == "windows" ? "Froststrap.exe" : "Froststrap")} --channel {channel} ");

        switch (os)
        {
            case "windows":
                args.Append($"--icon \"{GitRoot / "Froststrap" / "Froststrap.ico"}\" ");
                args.Append("--framework net10.0-x64-runtime,vcredist143-x64 ");
                args.Append("--msi "); // lowkey the exe format is way too minimal
                break;

            case "macos":
                args.Append("--bundleId xyz.froststrap.desktop ");
                if (string.Equals(Environment.GetEnvironmentVariable("SIGN"), "true", StringComparison.OrdinalIgnoreCase))
                {
                    args.Append($"--signAppIdentity \"{EnvironmentInfo.GetVariable<string>("DEVELOPER_ID_APP")}\" ");
                    args.Append($"--signInstallIdentity \"{EnvironmentInfo.GetVariable<string>("DEVELOPER_ID_INSTALLER")}\" ");
                    args.Append($"--signEntitlements \"{FalloutRoot / "Publish" / "macApp" / "Froststrap.entitlements"}\" ");
                    args.Append("--notaryProfile froststrap-notary ");
                }
                break;

            case "linux":
                args.Append($"--icon \"{FalloutRoot / "icon512.png"}\" ");
                break;
        }

        Log.Information("Packing with vpk on channel {Channel}", channel);
        RunProcess(Vpk(), args.ToString());
    }
}