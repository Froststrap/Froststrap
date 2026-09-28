using System;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;

public partial class Build : FalloutBuild
{
    void PublishMacOS()
    {
        var version = GitTag.TrimStart('v');
        AbsolutePath backendBuildRoot = GitRoot / "backend" / "target";
        AbsolutePath macAppLocation = FalloutRoot / "Publish" / "macApp";
        AbsolutePath xcodeProjectLocation = macAppLocation / "macApp.xcodeproj";
        AbsolutePath entitlementsPath = macAppLocation / "Froststrap.entitlements";
        AbsolutePath dylibDest = (AbsolutePath)DotnetPublishArtifactsDir / "libvirtualdisplay.dylib";

        var source = FindVirtualDisplayDylib(backendBuildRoot);
        Log.Information("Copying {Source} into {OutDir}", source, DotnetPublishArtifactsDir);
        File.Copy(source, dylibDest, overwrite: true);

        Log.Information("Building {xcproj} with xcodebuild", xcodeProjectLocation);
        var xcbProc = new Process();
        xcbProc.StartInfo.FileName = "xcodebuild";
        xcbProc.StartInfo.Arguments = $"-project {xcodeProjectLocation} " +
                                      "-target Froststrap " +
                                      $"-configuration {Configuration} " +
                                      $"MARKETING_VERSION=\"{version}\" " +
                                      $"CURRENT_PROJECT_VERSION=\"{version.Replace(".", "")}\" " +
                                      "CODE_SIGNING_ALLOWED=NO " +
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

        bool sign = string.Equals(
            Environment.GetEnvironmentVariable("SIGN"),
            "true",
            StringComparison.OrdinalIgnoreCase
        );

        if (sign)
        {
            SignAndNotarizeMacApp(dest, entitlementsPath, DistributionDir);
        }
        else
        {
            BuildUnsignedPkg(dest, DistributionDir);
        }
    }

    AbsolutePath FindVirtualDisplayDylib(AbsolutePath cargoTargetDir)
    {
        var profile = Configuration.ToString().Equals("Release", StringComparison.OrdinalIgnoreCase)
            ? "release" : "debug";
        var path = cargoTargetDir / profile / "libvirtualdisplay.dylib";

        if (!File.Exists(path))
            throw new Exception($"{path} not found - did `cargo build` run for the {profile} profile?");

        return path;
    }

    void SignAndNotarizeMacApp(AbsolutePath appPath, AbsolutePath entitlementsPath, string outputDirectory)
    {
        string developerIdApp = EnvironmentInfo.GetVariable<string>("DEVELOPER_ID_APP");
        string developerIdInstaller = EnvironmentInfo.GetVariable<string>("DEVELOPER_ID_INSTALLER");
        string appleKeyId = EnvironmentInfo.GetVariable<string>("APPLE_KEY_ID");
        string appleIssuerId = EnvironmentInfo.GetVariable<string>("APPLE_ISSUER_ID");

        AbsolutePath payloadDir = (AbsolutePath)outputDirectory / "payload";
        AbsolutePath payloadApplications = payloadDir / "Applications";
        AbsolutePath unsignedPkg = (AbsolutePath)outputDirectory / "Froststrap-unsigned.pkg";
        AbsolutePath finalPkg = (AbsolutePath)outputDirectory / "Froststrap.pkg";

        Log.Information("Signing .app with {DeveloperIdApp}", developerIdApp);
        RunProcess("codesign", $"--force --deep --options runtime --entitlements \"{entitlementsPath}\" --sign \"{developerIdApp}\" \"{appPath}\"");
        RunProcess("codesign", $"--verify --verbose=4 \"{appPath}\"");

        Directory.CreateDirectory(payloadApplications);
        Ditto(appPath, payloadApplications / "Froststrap.app");

        RunProcess("pkgbuild", $"--root \"{payloadDir}\" --install-location / --identifier xyz.froststrap.desktop \"{unsignedPkg}\"");

        Log.Information("Signing PKG with {DeveloperIdInstaller}", developerIdInstaller);
        RunProcess("productsign", $"--sign \"{developerIdInstaller}\" \"{unsignedPkg}\" \"{finalPkg}\"");

        Log.Information("Submitting for notarization...");
        AbsolutePath keysDir = (AbsolutePath)Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) / ".private_keys";
        Directory.CreateDirectory(keysDir);
        AbsolutePath keyPath = keysDir / $"AuthKey_{appleKeyId}.p8";

        string p8Content = EnvironmentInfo.GetVariable<string>("APP_STORE_CONNECT_P8_CONTENT");
        File.WriteAllText(keyPath, p8Content);

        var (exitCode, stdout, stderr) = RunProcessCaptured(
            "xcrun",
            $"notarytool submit \"{finalPkg}\" --key-id {appleKeyId} --issuer {appleIssuerId} --key \"{keyPath}\" --wait");

        string submissionOutput = stdout + stderr;
        Log.Information(submissionOutput);

        if (exitCode != 0)
        {
            Log.Error("notarytool exited with status {ExitCode} (see output above)", exitCode);
            throw new Exception("notarytool failed");
        }

        var idMatch = Regex.Match(submissionOutput, @"id:\s*(?<id>[a-f0-9-]+)");
        string submissionId = idMatch.Success ? idMatch.Groups["id"].Value : null;

        if (submissionOutput.Contains("status: Invalid"))
        {
            Log.Error("Notarization failed. Fetching detailed log...");
            RunProcess("xcrun", $"notarytool log {submissionId} --key-id {appleKeyId} --issuer {appleIssuerId} --key \"{keyPath}\"");
            throw new Exception("Notarization failed");
        }

        RunProcess("xcrun", $"stapler staple \"{finalPkg}\"");

        File.Delete(keyPath);
        Directory.Delete(payloadDir, recursive: true);
        File.Delete(unsignedPkg);
    }

    void BuildUnsignedPkg(AbsolutePath appPath, string outputDirectory)
    {
        Log.Information("Building unsigned PKG (skipping signing)");

        AbsolutePath payloadDir = (AbsolutePath)outputDirectory / "payload";
        AbsolutePath payloadApplications = payloadDir / "Applications";
        AbsolutePath finalPkg = (AbsolutePath)outputDirectory / "Froststrap.pkg";

        Directory.CreateDirectory(payloadApplications);
        Ditto(appPath, payloadApplications / "Froststrap.app");

        RunProcess("pkgbuild", $"--root \"{payloadDir}\" --install-location / --identifier xyz.froststrap.desktop \"{finalPkg}\"");

        Directory.Delete(payloadDir, recursive: true);
    }

    void Ditto(AbsolutePath src, AbsolutePath dest)
    {
        if (Directory.Exists(dest))
            Directory.Delete(dest, recursive: true);

        Directory.CreateDirectory(Path.GetDirectoryName((string)dest)!);
        RunProcess("ditto", $"\"{src}\" \"{dest}\"");
    }
}

