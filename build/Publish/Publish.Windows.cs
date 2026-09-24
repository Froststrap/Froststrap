using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;

public partial class Build : FalloutBuild
{
    const string VCRedistUrl = "https://aka.ms/vc14/vc_redist.x64.exe";

    string DownloadVCRedist()
    {
        string directory = System.IO.Path.Combine(OutputRoot, "vcredist");
        System.IO.Directory.CreateDirectory(directory);

        string destination = System.IO.Path.Combine(directory, "vc_redist.x64.exe");
        string temp = destination + ".download";

        try
        {
            Log.Information("Downloading VC++ redistributable from {Url}", VCRedistUrl);

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            using (var response = http.GetAsync(VCRedistUrl, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
            {
                response.EnsureSuccessStatusCode();
                using var source = response.Content.ReadAsStream();
                using var target = System.IO.File.Create(temp);
                source.CopyTo(target);
            }

            long size = new System.IO.FileInfo(temp).Length;
            if (size < 1_000_000)
                throw new Exception($"Downloaded file is only {size} bytes, expected the redistributable");

            System.IO.File.Move(temp, destination, overwrite: true);
            Log.Information("Saved VC++ redistributable to {Path}", destination);
        }
        catch (Exception ex)
        {
            if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp);

            if (!System.IO.File.Exists(destination))
                throw new Exception($"Could not download the VC++ redistributable from {VCRedistUrl}", ex);

            Log.Warning(ex, "VC++ redistributable download failed, using cached copy at {Path}", destination);
        }

        return destination;
    }

    void PublishWindows(string outputDirectory)
    {
        AbsolutePath nsiLocation = FalloutRoot / "Publish" / "WinNsis.nsi";
        string vcRedistPath = DownloadVCRedist();

        var version = GitTag.TrimStart('v');
        Log.Debug("Detected build version as {ver}", version);

        Log.Information("Building {nsi} with makensis", nsiLocation);
        RunProcess(
            "makensis",
            $"/DPUBLISH_DIR=\"{outputDirectory}\" /DAPP_VERSION=\"{version}\" /DVCREDIST=\"{vcRedistPath}\" /DSELFCONTAINED=1 \"{nsiLocation}\""
        );
    }
}
