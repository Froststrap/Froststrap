using System.Diagnostics;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Solutions;
using Microsoft.Build.Locator;
using Fallout.Common.Git;
using Serilog;

public partial class Build : FalloutBuild
{
    public static int Main() {
        MSBuildLocator.RegisterDefaults();
        return Execute<Build>(x => x.Compile);
    }

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    
    [GitRepository]
    readonly GitRepository Repository;

    [Solution]
    readonly Solution Solution;

    AbsolutePath GitRoot => Repository.LocalDirectory;
    AbsolutePath OutputRoot => GitRoot / ".build";

    Target BuildDebug => _ => _
        .Executes(() => {
            Log.Information("Git commit: {Value}", Repository.Commit);
            Log.Information("Git branch: {Value}", Repository.Branch);
            Log.Information("Git local dir: {Value}", GitRoot);
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
