using System.Diagnostics;
using System.IO;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;

public partial class Build : FalloutBuild
{
    void PublishMacOS(string outputDirectory)
	{
        AbsolutePath virtualbackendBuildRoot = GitRoot / "backend" / "virtualdisplay" / ".build";
        AbsolutePath macAppLocation = GitRoot / "packaging" / "macApp";
        AbsolutePath xcodeProjectLocation = macAppLocation / "macApp.xcodeproj";

        if (!File.Exists((AbsolutePath)outputDirectory / "libvirtualdisplay.dylib")
            && File.Exists($"{virtualbackendBuildRoot}/out/Products/Release/libvirtualdisplay.dylib"))
        {
            AbsolutePath source = $"{virtualbackendBuildRoot}/out/Products/Release/libvirtualdisplay.dylib";
            Log.Information("Copying over {Source} into {OutDir}", source, OutputRoot);
            File.Copy(source, (AbsolutePath)outputDirectory / "libvirtualdisplay.dylib");
        } else if (File.Exists($"{virtualbackendBuildRoot}/apple/Products/Release/libvirtualdisplay.dylib"))
        {
            AbsolutePath source = $"{virtualbackendBuildRoot}/apple/Products/Release/libvirtualdisplay.dylib";
            Log.Information("Copying over {Source} into {OutDir}", source, OutputRoot);
            File.Copy(source, (AbsolutePath)outputDirectory / "libvirtualdisplay.dylib");
        }

        Log.Information("Building {xcproj} with xcodebuild", xcodeProjectLocation);
        var xcbProc = new Process();
        xcbProc.StartInfo.FileName = "xcodebuild";

        xcbProc.StartInfo.Arguments = $"-project {xcodeProjectLocation} " +
                                      "-target Froststrap " +
                                      $"-configuration {Configuration} " +
                                      "build";

        xcbProc.StartInfo.UseShellExecute = false;

        xcbProc.Start();
        xcbProc.WaitForExit();

        var src = (AbsolutePath)macAppLocation / "build" / Configuration / "Froststrap.app";
        var dest = (AbsolutePath)outputDirectory / "Froststrap.app";
        Log.Information("Copying {src} artifact to {OutDir}", src, dest);
        
        var copyProc = new Process();
        copyProc.StartInfo.FileName = "cp";
        copyProc.StartInfo.Arguments = $"-r \"{(string)src}\" \"{(string)dest}\"";
        copyProc.StartInfo.UseShellExecute = false;

        copyProc.Start();
        copyProc.WaitForExit();
	}
}

