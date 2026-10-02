using System;
using System.Diagnostics;
using System.IO;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;

public partial class Build : FalloutBuild
{
    void PublishMacOS()
    {
        var version = GitTag.TrimStart('v');
        AbsolutePath macAppLocation = FalloutRoot / "Publish" / "macApp";
        AbsolutePath xcodeProjectLocation = macAppLocation / "macApp.xcodeproj";
        AbsolutePath entitlementsPath = macAppLocation / "Froststrap.entitlements";
        AbsolutePath dylibDest = (AbsolutePath)DotnetPublishArtifactsDir / "libvirtualdisplay.dylib";

        var VDLib = FindDylib("libvirtualdisplay.dylib");
        Log.Information("Copying {Source} into {OutDir}", VDLib, DotnetPublishArtifactsDir);
        File.Copy(VDLib, (AbsolutePath)DotnetPublishArtifactsDir / "libvirtualdisplay.dylib", overwrite: true);

        var MARLib = FindDylib("libmobileappreg.dylib");
        Log.Information("Copying {Source} into {OutDir}", MARLib, DotnetPublishArtifactsDir);
        File.Copy(MARLib, (AbsolutePath)DotnetPublishArtifactsDir / "libmobileappreg.dylib", overwrite: true);

        var notifyLib = FindDylib("libnotify.dylib");
        Log.Information("Copying {Source} into {OutDir}", notifyLib, DotnetPublishArtifactsDir);
        File.Copy(notifyLib, (AbsolutePath)DotnetPublishArtifactsDir / "libnotify.dylib", overwrite: true);

        Log.Information("Building {xcproj} with xcodebuild", xcodeProjectLocation);
        string xcodeArch = TargetArch == "x64" ? "x86_64" : "arm64";

        var xcbProc = new Process();
        xcbProc.StartInfo.FileName = "xcodebuild";
        xcbProc.StartInfo.Arguments = $"-project {xcodeProjectLocation} " +
                                      "-target Froststrap " +
                                      $"-configuration {Configuration} " +
                                      $"MARKETING_VERSION=\"{version}\" " +
                                      $"CURRENT_PROJECT_VERSION=\"{version.Replace(".", "")}\" " +
                                      "CODE_SIGNING_ALLOWED=NO " +
                                      $"ARCHS={xcodeArch} " +
                                      "ONLY_ACTIVE_ARCH=NO " +
                                      "build";
        xcbProc.StartInfo.UseShellExecute = false;
        xcbProc.Start();
        xcbProc.WaitForExit();

        if (xcbProc.ExitCode != 0)
        {
            Log.Error("xcodebuild failed with exit code {ExitCode}", xcbProc.ExitCode);
            throw new Exception("xcodebuild failed");
        }

        System.IO.Directory.CreateDirectory(DistributionDir);
        var src = (AbsolutePath)macAppLocation / "build" / Configuration / "Froststrap.app";
        var dest = (AbsolutePath)DistributionDir / "Froststrap.app";
        Log.Information("Copying {src} artifact to {OutDir}", src, dest);
        Ditto(src, dest);

        if (NoInstallers) return;
        PackVelopack(dest);
    }

    AbsolutePath FindDylib(string fileName)
    {
        var cargoTargetDir = GitRoot / "backend" / "target";
        var profile = Configuration.ToString().Equals("Release", StringComparison.OrdinalIgnoreCase)
            ? "release" : "debug";
        var path = cargoTargetDir / RustTargetTriple / profile / fileName;

        if (!File.Exists(path))
            throw new Exception($"{path} not found - did `cargo build` run for the {profile} profile on {RustTargetTriple}?");

        return path;
    }

    void Ditto(AbsolutePath src, AbsolutePath dest)
    {
        if (Directory.Exists(dest))
            Directory.Delete(dest, recursive: true);

        Directory.CreateDirectory(Path.GetDirectoryName((string)dest)!);
        RunProcess("ditto", $"\"{src}\" \"{dest}\"");
    }
}
