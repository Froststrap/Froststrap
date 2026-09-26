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
                                      $"-o \"{DotnetPublishArtifactsDir}\" " +
                                      $"-p:PublishProfile=\"{publishProfile}\" " +
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
