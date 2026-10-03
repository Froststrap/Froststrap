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
        AbsolutePath icon      = FalloutRoot / "icon512.png";
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
        PackVelopack();
    }

    void BuildNFPM(AbsolutePath outputDir, string version, AbsolutePath desktop, AbsolutePath icon)
    {
        string nfpm = EnsureTool(DistributionDir, "nfpm",
            "https://github.com/goreleaser/nfpm/releases/download/v2.47.0/nfpm_2.47.0_Linux_x86_64.tar.gz", extractTarGz: true);

        AbsolutePath binary = outputDir / "Froststrap";
        AbsolutePath config = DistributionDir / "nfpm.yaml";
        string postinstallPath = DistributionDir / "nfpm-postinstall.sh";

        string postinstall = """
            #!/bin/sh
            set -e

            if command -v update-desktop-database >/dev/null 2>&1; then
              update-desktop-database -q /usr/share/applications || :
            fi

            if command -v gtk-update-icon-cache >/dev/null 2>&1; then
              gtk-update-icon-cache -q /usr/share/icons/hicolor || :
            fi

            /usr/bin/Froststrap --register-mime-types 2>/dev/null || :
            """;
        File.WriteAllText(postinstallPath, postinstall);

        var nfpmYaml = $"""
            name: froststrap
            arch: {(TargetArch == "arm64" ? "arm64" : "amd64")}
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
              postinstall: {postinstallPath}
            """;

        File.WriteAllText(config, nfpmYaml);

        foreach (var packager in new[] { "deb", "rpm" })
        {
            Log.Information("Building .{pkg} via nFPM", packager);
            RunProcess(nfpm,
                $"pkg --packager {packager} -f \"{config}\" " +
                $"-t \"{DistributionDir / $"Froststrap-linux-{TargetArch}.{packager}"}\"");
        }
    }

    string EnsureTool(AbsolutePath buildDir, string name, string url, bool extractTarGz = false)
    {
        if (IsOnPath(name)) return name;

        AbsolutePath toolPath = buildDir / name;
        if (File.Exists(toolPath))
            return toolPath;

        if (extractTarGz)
        {
            AbsolutePath archivePath = buildDir / $"{name}.tar.gz";
            AbsolutePath extractDir  = buildDir / $"{name}-extracted";

            Log.Information("{tool} not found on PATH, downloading archive to {path}", name, archivePath);
            RunProcess("curl", $"-L --fail -o \"{archivePath}\" \"{url}\"");

            Directory.CreateDirectory(extractDir);
            RunProcess("tar", $"-xzf \"{archivePath}\" -C \"{extractDir}\"");

            File.Copy(extractDir / name, toolPath, overwrite: true);
            File.Delete(archivePath);
            Directory.Delete(extractDir, recursive: true);
        }
        else
        {
            Log.Information("{tool} not found on PATH, downloading to {path}", name, toolPath);
            RunProcess("curl", $"-L --fail -o \"{toolPath}\" \"{url}\"");
        }

        RunProcess("chmod", $"+x \"{toolPath}\"");

        return toolPath;
    }

    static bool IsOnPath(string exe) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Any(d => File.Exists(Path.Combine(d, exe)));

}
