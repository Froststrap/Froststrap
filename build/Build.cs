using System.Diagnostics;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Solutions;
using Microsoft.Build.Locator;
using Fallout.Common.Git;
using Serilog;
using System;
using System.Linq;

public partial class Build : FalloutBuild
{
    [Parameter("Skip building installers")]
    readonly bool NoInstallers;

    [GitRepository]
    readonly GitRepository Repository;

    AbsolutePath GitRoot => Repository.LocalDirectory;
    AbsolutePath FalloutRoot => GitRoot / "build";
    AbsolutePath OutputRoot => GitRoot / ".build";
    string GitTag;

    public static int Main() {
        MSBuildLocator.RegisterDefaults();
        return Execute<Build>(x => x.Compile);
    }

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    
    [Solution]
    readonly Solution Solution;

    Target BuildDebug => _ => _
        .Executes(() =>
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "tag",
                    WorkingDirectory = GitRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            var tags = output
                .Split(
                    '\n',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);

            GitTag = tags.LastOrDefault();

            Log.Information("Git commit: {Value}", Repository.Commit);
            Log.Information("Git branch: {Value}", Repository.Branch);
            Log.Information("Git local dir: {Value}", GitRoot);
            Log.Information("Git tag: {Value}", GitTag ?? "");
            Console.WriteLine(); // seperator
            Log.Information("No Installers: {Value}", NoInstallers);
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
        .Executes(() => PublishMain(NoInstallers));

    Target Compile => _ => _
        .DependsOn(Restore)
        .DependsOn(BuildDebug)
        .Executes(() => CompileMain());
}
