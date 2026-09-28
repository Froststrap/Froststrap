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

        BuildNFPM(outputDir, version, desktop, icon);
        BuildAppImage(outputDir, appDir, desktop, icon, version);
    }

    void BuildNFPM(AbsolutePath outputDir, string version, AbsolutePath desktop, AbsolutePath icon)
    {
        string nfpm = EnsureTool(outputDir, "nfpm",
            "https://github.com/goreleaser/nfpm/releases/download/v2.47.0/nfpm_2.47.0_amd64.deb", extractDeb: true);

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

    void BuildAppImage(AbsolutePath outputDir, AbsolutePath appDir, AbsolutePath desktop, AbsolutePath icon, string version)
    {
        if (IsNix())
        {
            Log.Warning("Nix detected, appimagetool doesn't fare well here, so skipping this step.");
            return;
        }

        if (Directory.Exists(appDir))
            Directory.Delete(appDir, recursive: true);

        Directory.CreateDirectory(appDir / "usr" / "bin");
        Directory.CreateDirectory(appDir / "usr" / "share" / "applications");
        Directory.CreateDirectory(appDir / "usr" / "share" / "icons" / "hicolor" / "512x512" / "apps");

        File.Copy(outputDir / "Froststrap", appDir / "usr" / "bin" / "Froststrap", overwrite: true);
        RunProcess("chmod", $"+x \"{appDir / "usr" / "bin" / "Froststrap"}\"");

        AbsolutePath icon512 = DistributionDir / "froststrap-512.png";
        RunProcess("magick", $"\"{icon}\" -resize 512x512 \"{icon512}\"");

        File.Copy(icon512, appDir / "froststrap.png", overwrite: true);
        File.Copy(icon512, appDir / "usr" / "share" / "icons" / "hicolor" / "512x512" / "apps" / "froststrap.png", overwrite: true);

        File.Copy(desktop, appDir / "Froststrap.desktop", overwrite: true);
        File.Copy(desktop, appDir / "usr" / "share" / "applications" / "Froststrap.desktop", overwrite: true);

        var appRun = """
        #!/bin/sh
        HERE="$(dirname "$(readlink -f "$0")")"
        exec "$HERE/usr/bin/Froststrap" "$@"
        """;

        File.WriteAllText(appDir / "AppRun", appRun);
        RunProcess("chmod", $"+x \"{appDir / "AppRun"}\"");

        string tool = "appimagetool";

        if (!IsOnPath("appimagetool"))
        {
            AbsolutePath toolPath = DistributionDir / "appimagetool.AppImage";
            Log.Information("appimagetool not found on PATH, downloading to {path}", toolPath);
            RunProcess("curl",
                $"-L --fail -o \"{toolPath}\" " +
                "https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage");
            RunProcess("chmod", $"+x \"{toolPath}\"");
            tool = toolPath;
        }

        Environment.SetEnvironmentVariable("ARCH", "x86_64");
        Environment.SetEnvironmentVariable("SOURCE_DATE_EPOCH", null);

        Log.Information("Building AppImage");
        RunProcess(tool,
            $"--appimage-extract-and-run \"{appDir}\" \"{DistributionDir / "Froststrap-linux-x64.AppImage"}\"");

        Directory.Delete(appDir, recursive: true);
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

    static bool IsNix() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IN_NIX_SHELL"))
        || Directory.Exists("/nix/store");
}
