using System;
using System.Diagnostics;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Solutions;
using Microsoft.Build.Locator;
using Fallout.Common.Git;
using Serilog;
using System.Runtime.InteropServices;
using System.IO;

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
        .Executes(() => {
            string outputDirectory = System.IO.Path.Combine(OutputRoot, "publish");
            System.IO.Directory.CreateDirectory(outputDirectory);
            System.IO.File.WriteAllText(System.IO.Path.Combine(OutputRoot, ".gitignore"), "*");
            
            var project = Solution.GetProject("Froststrap");
            Log.Information("Froststrap path: {Value}", project.Directory);
            Log.Information("Publishing {Value}...", project.Path);
            Log.Information("Artifacts will output to: {Value}", outputDirectory);

            string arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                _ => null
            };

            string rid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"win-{arch}" :
                        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? $"linux-{arch}" :
                        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"osx-{arch}" : null;

            string publish = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"windows-{arch}" :
                        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? $"linux-{arch}" :
                        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"osx-{arch}" : null;

            if (rid == null || arch == null || publish == null)
            {
                throw new PlatformNotSupportedException("Unsupported OS or Architecture for publishing.");
            }

            string publishProfile = $"Publish-{publish}";

            Log.Information("Publishing for {Rid} using profile {Profile}", rid, publishProfile);

            var process = new Process();
            process.StartInfo.FileName = "dotnet";

            process.StartInfo.Arguments = $"publish \"{project.Path}\" " +
                                          $"-c {Configuration} " +
                                          $"-r {rid} " +
                                          $"-o \"{outputDirectory}\" " +
                                          $"--configfile \"{GitRoot}/nuget.config\" " +
                                          $"-p:PublishProfile=\"{publishProfile}\" " +
                                          $"--nologo";
                                          
            process.StartInfo.UseShellExecute = false;
            
            process.Start();
            process.WaitForExit();

            AbsolutePath virtualbackendBuildRoot = $"{GitRoot}/backend/virtualdisplay/.build";

            if (File.Exists($"{virtualbackendBuildRoot}/out/Products/Release/libvirtualdisplay.dylib"))
            {
                AbsolutePath source = $"{virtualbackendBuildRoot}/out/Products/Release/libvirtualdisplay.dylib";
                Log.Information("Copying over {Source} into {OutDir}", source, OutputRoot);
                File.Copy(source, (AbsolutePath)outputDirectory / "libvirtualdisplay.dylib");
            }
            if (File.Exists($"{virtualbackendBuildRoot}/apple/Products/Release/libvirtualdisplay.dylib"))
            {
                AbsolutePath source = $"{virtualbackendBuildRoot}/apple/Products/Release/libvirtualdisplay.dylib";
                Log.Information("Copying over {Source} into {OutDir}", source, OutputRoot);
                File.Copy(source, (AbsolutePath)outputDirectory / "libvirtualdisplay.dylib");
            }

            foreach (string file in Directory.EnumerateFiles(outputDirectory))
            {
                // delete these, debug outputs aren't needed
                if (file.EndsWith(".pdb")) {
                    Log.Information("Deleting debug file {FileName}...", file);
                    File.Delete(file);
                }
            }

            if (process.ExitCode != 0)
            {
                throw new Exception($"Publish failed for {rid} with exit code {process.ExitCode}");
            }
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
