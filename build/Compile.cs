using System;
using System.Diagnostics;
using Fallout.Common;
using Fallout.Solutions;
using Serilog;

public partial class Build : FalloutBuild
{
	void CompileMain()
	{
        System.IO.Directory.CreateDirectory(DotnetBuildArtifactsDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(OutputRoot, ".gitignore"), "*");

        var project = Solution.GetProject("Froststrap");
        Log.Information("Froststrap path: {Value}", project.Directory);
        Log.Information("Building {Value}...", project.Path);
        Log.Information("Artifacts will output to: {Value}", DotnetBuildArtifactsDir);

        var process = new Process();
        process.StartInfo.FileName = "dotnet";
        
        process.StartInfo.Arguments = $"msbuild \"{project.Path}\" " +
                                      $"-p:Configuration={Configuration} " +
                                      $"-p:OutputPath=\"{DotnetBuildArtifactsDir}\" " +
                                      $"-nologo";
                                      
        process.StartInfo.UseShellExecute = false;
        
        process.Start();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception($"MSBuild failed with exit code {process.ExitCode}");
        }
	}
}
