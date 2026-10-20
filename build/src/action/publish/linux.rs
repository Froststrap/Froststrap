//! os specific stuff for publish

use crate::{args, arch, path::{self, dist, dotnet_publish_artifacts}, utils::{dedent, log, process}};
use std::{fs, path::{PathBuf, Path}, fs::File, io::Write};

pub fn main() {
    let args = args::get();
    if args.no_installers {
        return;
    }
    let version = super::version();
    let icon = path::xtask_root().join("icon512.png");
    let desktop = path::dist().join("Froststrap.desktop");

    std::fs::create_dir_all(path::dist()).unwrap();

    let entry = desktop_entry(&version);
    let mut desktop_file = File::create(desktop.clone()).unwrap();
    desktop_file.write_all(entry.as_bytes()).unwrap();

    build_nfpm(dotnet_publish_artifacts(), version, desktop, icon);
    super::velopack::pack(&dotnet_publish_artifacts(), None);
}

fn desktop_entry(version: &str) -> String {
    dedent(&format!(
        r#"
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
    "#
    ))
}

fn build_nfpm(
    output_dir: PathBuf,
    version: String,
    desktop: PathBuf,
    icon: PathBuf,
) {
    let nfpm = ensure_tool(&path::dist(), "nfpm",
        "https://github.com/goreleaser/nfpm/releases/download/v2.47.0/nfpm_2.47.0_Linux_x86_64.tar.gz", true);

    let binary = output_dir.join("Froststrap");
    let config = dist().join("nfpm.yaml");
    let post_install_path = dist().join("nfpm-postinstall.sh");

    let script = post_install_script();
    let mut post_install_file = File::create(post_install_path.clone()).unwrap();
    post_install_file.write_all(script.as_bytes()).unwrap();

    let nfpm_yaml = nfpm_config(
        arch::nfpm_arch(),
        &binary,
        &icon,
        &desktop,
        &post_install_path,
        &version,
    );

    let mut nfpm_yaml_file = File::create(config.clone()).unwrap();
    nfpm_yaml_file.write_all(nfpm_yaml.as_bytes()).unwrap();

    for p in ["deb", "rpm", "archlinux"] {
        let out_name = match p {
            "archlinux" => format!("Froststrap-linux-{}-{}.pkg.tar.zst", arch::target_arch(), p),
            _ => format!("Froststrap-linux-{}.{}", arch::target_arch(), p),
        };
        
        log::info!("Building .{} via nFPM", p);

        let target = dist()
            .join(out_name)
            .to_string_lossy()
            .into_owned();
        let config_s = config.to_string_lossy().into_owned();

        let args = ["pkg", "--packager", p, "-f", &config_s, "-t", &target];
        process(&nfpm, args).exec_checked(&nfpm);
    }
}

fn post_install_script() -> String {
    dedent(
        r#"
        #!/bin/sh
        set -e

        if command -v update-desktop-database >/dev/null 2>&1; then
          update-desktop-database -q /usr/share/applications || :
        fi

        if command -v gtk-update-icon-cache >/dev/null 2>&1; then
          gtk-update-icon-cache -q /usr/share/icons/hicolor || :
        fi

        /usr/bin/Froststrap --register-mime-types 2>/dev/null || :
    "#,
    )
}

fn nfpm_config(
    arch: &str,
    binary: &Path,
    icon: &Path,
    desktop: &Path,
    post_install: &Path,
    version: &str,
) -> String {
    dedent(&format!(
        r#"
            name: froststrap
            arch: {arch}
            platform: linux
            version: {version}
            license: MPL-2.0
            maintainer: Froststrap
            description: Roblox bootstrapper and mod manager

            overrides:
              deb:
                depends:
                  - libicu-dev
              rpm:
                depends:
                  - libicu
              archlinux:
                depends:
                  - icu
            
            depends:
              - dbus

            contents:
              - src: {}
                dst: /usr/bin/Froststrap
                file_info:
                  mode: 0755
              - src: {}
                dst: /usr/share/icons/hicolor/512x512/apps/froststrap.png
              - src: {}
                dst: /usr/share/applications/Froststrap.desktop

            scripts:
              postinstall: {}
        "#,
        binary.to_string_lossy(),
        icon.to_string_lossy(),
        desktop.to_string_lossy(),
        post_install.to_string_lossy()
    ))
}

pub fn ensure_tool(build_dir: &Path, name: &str, url: &str, extract_tar_gz: bool) -> String {
    if is_on_path(name) {
        return name.to_string();
    }

    let tool_path = build_dir.join(name);
    let tool_s = tool_path.to_string_lossy().into_owned();
    if tool_path.is_file() {
        return tool_s;
    }

    if extract_tar_gz {
        let archive = build_dir.join(format!("{name}.tar.gz"));
        let extract_dir = build_dir.join(format!("{name}-extracted"));
        let archive_s = archive.to_string_lossy().into_owned();
        let extract_s = extract_dir.to_string_lossy().into_owned();

        log::info!("{} not found on PATH, downloading archive to {}", name, archive_s);
        process("curl", ["-L", "--fail", "-o", &archive_s, url]).exec_checked("curl");

        fs::create_dir_all(&extract_dir).unwrap();
        process("tar", ["-xzf", &archive_s, "-C", &extract_s]).exec_checked("tar");

        fs::copy(extract_dir.join(name), &tool_path).unwrap();
        fs::remove_file(&archive).unwrap();
        fs::remove_dir_all(&extract_dir).unwrap();
    } else {
        log::info!("{} not found on PATH, downloading to {}", name, tool_s);
        process("curl", ["-L", "--fail", "-o", &tool_s, url]).exec_checked("curl");
    }

    #[cfg(unix)]
    {
        use std::os::unix::fs::PermissionsExt;
        fs::set_permissions(&tool_path, fs::Permissions::from_mode(0o755)).unwrap();
    }

    tool_s
}

pub fn is_on_path(exe: &str) -> bool {
    let Some(path) = std::env::var_os("PATH") else {
        return false;
    };
    std::env::split_paths(&path)
        .filter(|dir| !dir.as_os_str().is_empty())
        .any(|dir| dir.join(exe).exists())
}
