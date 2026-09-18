using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;

public partial class Build : FalloutBuild
{
    void PublishWindows(string outputDirectory)
    {
        AbsolutePath nsiLocation = GitRoot / "packaging" / "WinNsis.nsi";

        var (_, versionStdout, _) = RunProcessCaptured(
            "git",
            "describe --tags --abbrev=0"
        );

        var version = versionStdout.Trim().TrimStart('v');
        Log.Debug("Detected build version as {ver}", version);

        Log.Information("Building {nsi} with makensis", nsiLocation);
        RunProcess(
            "makensis",
            $"/DPUBLISH_DIR=\"{outputDirectory}\" /DAPP_VERSION=\"{version}\" /DSELFCONTAINED=1 \"{nsiLocation}\""
        );
    }
}
