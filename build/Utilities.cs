using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;

public partial class Build : FalloutBuild
{
    void RunProcess(string fileName, string arguments)
    {
        var proc = new Process();
        proc.StartInfo.FileName = fileName;
        proc.StartInfo.Arguments = arguments;
        proc.StartInfo.UseShellExecute = false;
        proc.Start();
        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            Log.Error("{FileName} exited with code {ExitCode}", fileName, proc.ExitCode);
            throw new Exception($"{fileName} failed with exit code {proc.ExitCode}");
        }
    }

    (int ExitCode, string Stdout, string Stderr) RunProcessCaptured(string fileName, string arguments)
    {
        var proc = new Process();
        proc.StartInfo.FileName = fileName;
        proc.StartInfo.Arguments = arguments;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.RedirectStandardError = true;
        proc.Start();

        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        return (proc.ExitCode, stdout, stderr);
    }
}
