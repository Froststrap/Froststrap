using System;
using System.Diagnostics;
using System.Linq;
using Fallout.Common;
using Fallout.Common.CI;
using Fallout.Common.Execution;
using Fallout.Common.IO;
using Fallout.Solutions;
using Fallout.Common.Tooling;
using Fallout.Common.Utilities.Collections;
using static Fallout.Common.EnvironmentInfo;
using static Fallout.Common.IO.PathConstruction;
using Microsoft.Build.Locator;
using Fallout.Common.Git;
using Serilog;

class Build : FalloutBuild
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
            if (System.IO.Directory.Exists(OutputRoot))
            {
                System.IO.Directory.Delete(OutputRoot, recursive: true);
            }
        });

    Target Restore => _ => _
        .Executes(() =>
        {
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .DependsOn(BuildDebug)
        .Executes(() =>
        {
            string outputDirectory = System.IO.Path.Combine(OutputRoot, "msbuild");
            System.IO.Directory.CreateDirectory(outputDirectory);
            System.IO.File.WriteAllText(System.IO.Path.Combine(OutputRoot, ".gitignore"), "*");

            var project = Solution.GetProject("Froststrap");
            Log.Information("Froststrap path: {Value}", project.Directory);
            Log.Information("Building {Value}...", project.Path);
            Log.Information("Artifacts will output to: {Value}", outputDirectory);

            var process = new Process();
            process.StartInfo.FileName = "dotnet";
            
            process.StartInfo.Arguments = $"msbuild \"{project.Path}\" " +
                                          $"-p:Configuration={Configuration} " +
                                          $"-p:OutputPath=\"{outputDirectory}\" " +
                                          $"-nologo";
                                          
            process.StartInfo.UseShellExecute = false;
            
            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"MSBuild failed with exit code {process.ExitCode}");
            }
        });
}
