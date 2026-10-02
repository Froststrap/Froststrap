using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;

public partial class Build : FalloutBuild
{
    [Parameter("Markdown release notes file embedded into the Velopack feed")]
    readonly string ReleaseNotes;

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
        bool win = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        bool mac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        string channel = win && TargetArch == "x64" ? "windows-x64" :
                         mac && (TargetArch is "x64" or "arm64") ? $"macos-{TargetArch}" :
                         !win && !mac && TargetArch == "x64" ? "linux-x64" :
                         throw new PlatformNotSupportedException(
                             $"Velopack does not support channel for {RuntimeInformation.OSDescription} {TargetArch}.");

        Directory.CreateDirectory(VelopackDir);

        var args = new StringBuilder();
        args.Append($"pack --packId Froststrap --packTitle Froststrap --packAuthors Froststrap ");
        args.Append($"--packVersion \"{version}\" --packDir \"{packDir}\" --outputDir \"{VelopackDir}\" ");
        args.Append($"--mainExe {(win ? "Froststrap.exe" : "Froststrap")} --channel {channel} ");

        if (!string.IsNullOrEmpty(ReleaseNotes) && File.Exists(ReleaseNotes))
            args.Append($"--releaseNotes \"{ReleaseNotes}\" ");

        if (win)
        {
            args.Append($"--icon \"{GitRoot / "Froststrap" / "Froststrap.ico"}\" ");
            args.Append("--framework vcredist143-x64 "); // replaces the NSIS VC++ redist logic
        }
        else if (mac)
        {
            args.Append("--bundleId xyz.froststrap.desktop ");
            if (string.Equals(Environment.GetEnvironmentVariable("SIGN"), "true", StringComparison.OrdinalIgnoreCase))
            {
                args.Append($"--signAppIdentity \"{EnvironmentInfo.GetVariable<string>("DEVELOPER_ID_APP")}\" ");
                args.Append($"--signInstallIdentity \"{EnvironmentInfo.GetVariable<string>("DEVELOPER_ID_INSTALLER")}\" ");
                args.Append($"--signEntitlements \"{FalloutRoot / "Publish" / "macApp" / "Froststrap.entitlements"}\" ");
                args.Append("--notaryProfile froststrap-notary ");
            }
        }
        else
        {
            args.Append($"--icon \"{FalloutRoot / "icon512.png"}\" ");
        }

        Log.Information("Packing with vpk on channel {Channel}", channel);
        RunProcess(Vpk(), args.ToString());
    }
}
