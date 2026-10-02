using System.Diagnostics;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Solutions;
using Microsoft.Build.Locator;
using Fallout.Common.Git;
using Serilog;
using System;

public partial class Build : FalloutBuild
{
    [Parameter("Skip building installers")]
    readonly bool NoInstallers;
    string GitTag;

    [GitRepository]
    readonly GitRepository Repository;

    AbsolutePath GitRoot => Repository.LocalDirectory;
    AbsolutePath FalloutRoot => GitRoot / "build";
    AbsolutePath OutputRoot => GitRoot / ".build";

    AbsolutePath DotnetPublishArtifactsDir => OutputRoot / "publish";
    AbsolutePath DotnetBuildArtifactsDir => OutputRoot / "build";
    AbsolutePath BundlingArtifactsDir => OutputRoot / "bundling";
    AbsolutePath DistributionDir => OutputRoot / "dist";

    public static int Main() {
        MSBuildLocator.RegisterDefaults();
        return Execute<Build>(x => x.Compile);
    }

    [Parameter("Configuration to build - Default is Release")]
    readonly Configuration Configuration = Configuration.Release;

    [Parameter("Override the target architecture for cross-compiling (x64 or arm64)")]
    readonly string OverrideArch;
    
    [Solution]
    readonly Solution Solution;

    string ResolveGitTag()
    {
        string tag;
        if (Environment.GetEnvironmentVariable("GITHUB_REF_TYPE") == "tag")
        {
            tag = Environment.GetEnvironmentVariable("GITHUB_REF_NAME");
        }
        else
        {
            var (exitCode, stdout, _) = RunProcessCaptured("git", "describe --tags --abbrev=0");
            tag = exitCode == 0 ? stdout.Trim() : "v0.0.0";

            string runNumber = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
            if (!string.IsNullOrWhiteSpace(runNumber))
                tag = $"{tag}-ci.{runNumber}";
        }

        return string.IsNullOrWhiteSpace(tag) ? "v0.0.0" : tag;
    }

    Target BuildDebug => _ => _
        .Executes(() =>
        {
            GitTag = ResolveGitTag();

            Log.Information("Git commit: {Value}", Repository.Commit);
            Log.Information("Git branch: {Value}", Repository.Branch);
            Log.Information("Git local dir: {Value}", GitRoot);
            Log.Information("Git tag: {Value}", GitTag ?? "");
            Console.WriteLine(); // seperator
            Log.Information("No Installers: {Value}", NoInstallers);
            Log.Information("Configuration: {Value}", Configuration);
            Log.Information("Override Arch: {Value}", OverrideArch ?? "(host)");
            Log.Information("Target Arch: {Value}", TargetArch);
            Log.Information("Rust Target: {Value}", RustTargetTriple);
        });

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            if (System.IO.Directory.Exists(OutputRoot)) System.IO.Directory.Delete(OutputRoot, recursive: true);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            var process = new Process();
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.Arguments = "restore";         
            process.StartInfo.UseShellExecute = false;            
            process.Start();
            process.WaitForExit();
        });

    Target Publish => _ => _
        .DependsOn(Restore)
        .DependsOn(BuildDebug)
        .Executes(() => PublishMain());

    Target Compile => _ => _
        .DependsOn(Restore)
        .DependsOn(BuildDebug)
        .Executes(() => CompileMain());
}
