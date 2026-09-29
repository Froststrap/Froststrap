using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Fallout.Common;
using Fallout.Solutions;
using Serilog;

public partial class Build : FalloutBuild
{   
    void PublishMain()
    {
        Directory.CreateDirectory(DotnetPublishArtifactsDir);
        File.WriteAllText(Path.Combine(OutputRoot, ".gitignore"), "*");

        var project = Solution.GetProject("Froststrap");
        Log.Information("Froststrap path: {Value}", project.Directory);
        Log.Information("Publishing {Value} to {Value}...", project.Path, DotnetPublishArtifactsDir);

        string rid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"win-{TargetArch}" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? $"linux-{TargetArch}" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"osx-{TargetArch}" : null;

        if (rid == null)
        {
            throw new PlatformNotSupportedException("Unsupported OS or Architecture for publishing.");
        }

        Log.Information("Publishing for {Rid}", rid);

        var process = new Process();
        process.StartInfo.FileName = "dotnet";

        process.StartInfo.Arguments = $"publish \"{project.Path}\" " +
                                      $"-c {Configuration} " +
                                      $"-r {rid} " +
                                      $"-o \"{DotnetPublishArtifactsDir}\" " +
                                      $"-p:RustTargetTriple={RustTargetTriple} " +
                                      $"-p:AppVersion=\"{GitTag.TrimStart('v')}\" " +
                                      $"--nologo";

        process.StartInfo.UseShellExecute = false;

        process.Start();
        process.WaitForExit();

        foreach (string file in Directory.EnumerateFiles(DotnetPublishArtifactsDir))
        {
            if (file.EndsWith(".pdb"))
            {
                Log.Information("Deleting debug file {FileName}...", file);
                File.Delete(file);
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) PublishMacOS();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) PublishWindows();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) PublishLinux();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Publish failed for {rid} with exit code {process.ExitCode}");
        } else {
            Log.Information("Build complete");
        }
    }
}
