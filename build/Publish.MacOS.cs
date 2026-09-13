using System.IO;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;

public partial class Build : FalloutBuild
{
    void PublishMacOS(string outputDirectory)
	{
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
	}
}

