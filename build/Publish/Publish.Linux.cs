using System;
using System.IO;
using System.Linq;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;

public partial class Build : FalloutBuild
{
    void PublishLinux()
    {
        if (NoInstallers) return;

        var version = GitTag.TrimStart('v');
        Log.Debug("Detected build version as {ver}", version);

        AbsolutePath outputDir = DotnetPublishArtifactsDir;
        AbsolutePath appDir    = DistributionDir / "AppDir";
        AbsolutePath icon      = GitRoot / "Froststrap" / "Froststrap.png";
        AbsolutePath desktop   = DistributionDir / "Froststrap.desktop";

        Directory.CreateDirectory(DistributionDir);

        var desktopEntry = $"""
            [Desktop Entry]
            Type=Application
            Name=Froststrap
            Comment=A fork of Fishstrap, focused on performance and customization
            Exec=Froststrap %u
            TryExec=Froststrap
            Icon=froststrap
            Terminal=false
            Categories=Game;
            MimeType=x-scheme-handler/roblox;x-scheme-handler/roblox-player;
            X-AppImage-Version={version}
            """;

        File.WriteAllText(desktop, desktopEntry);

        BuildDebRpm(outputDir, version, desktop, icon);
    }

    void BuildDebRpm(AbsolutePath outputDir, string version, AbsolutePath desktop, AbsolutePath icon)
    {
        string nfpm = EnsureTool(outputDir, "nfpm",
            "https://github.com/goreleaser/nfpm/releases/latest/download/nfpm_amd64.deb", extractDeb: true);

        AbsolutePath binary = outputDir / "Froststrap";
        AbsolutePath config = DistributionDir / "nfpm.yaml";

        var nfpmYaml = $"""
            name: froststrap
            arch: amd64
            platform: linux
            version: {version}
            maintainer: Froststrap-Dev
            description: Roblox bootstrapper and mod manager
            depends:
              - libicu-dev

            contents:
              - src: {binary}
                dst: /usr/bin/Froststrap
                file_info:
                  mode: 0755
              - src: {icon}
                dst: /usr/share/icons/hicolor/512x512/apps/froststrap.png
              - src: {desktop}
                dst: /usr/share/applications/Froststrap.desktop

            scripts:
              postinstall: {FalloutRoot / "Publish" / "nfpm-postinstall.sh"}
            """;

        File.WriteAllText(config, nfpmYaml);

        foreach (var packager in new[] { "deb", "rpm" })
        {
            Log.Information("Building .{pkg} via nFPM", packager);
            RunProcess(nfpm,
                $"pkg --packager {packager} -f \"{config}\" " +
                $"-t \"{DistributionDir / $"Froststrap-linux-x64.{packager}"}\"");
        }
    }
    string EnsureTool(AbsolutePath buildDir, string name, string url, bool extractDeb = false)
    {
        if (IsOnPath(name)) return name;

        AbsolutePath toolPath = buildDir / name;
        if (!File.Exists(toolPath))
        {
            Log.Information("{tool} not found on PATH, downloading to {path}", name, toolPath);
            RunProcess("curl", $"-L --fail -o \"{toolPath}\" \"{url}\"");
            RunProcess("chmod", $"+x \"{toolPath}\"");
        }
        return toolPath;
    }

    static bool IsOnPath(string exe) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Any(d => File.Exists(Path.Combine(d, exe)));
}
