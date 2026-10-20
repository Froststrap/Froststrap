//! controller module for velopack

use crate::{arch, path, utils::{log, process}};
use std::{path::PathBuf, env::consts};

#[cfg(target_os = "macos")]
use std::env;

#[cfg(target_os = "macos")]
fn required_env(key: &str) -> String {
    env::var(key).unwrap_or_else(|_| {
        log::error!("missing required environment variable {key}");
        std::process::exit(1);
    })
}

pub fn vpk() -> String {
    let exe = if cfg!(windows) { "vpk.exe" } else { "vpk" };

    if let Some(home) = std::env::home_dir() {
        let local: PathBuf = home.join(".dotnet").join("tools").join(exe);
        if local.is_file() {
            return local.to_string_lossy().into_owned();
        }
    }

    "vpk".to_string()
}

pub fn pack(pack_dir: &std::path::Path, entitlements: Option<&std::path::Path>) {
    _ = entitlements; // shut up warning on other systems
    let pack_path = pack_dir.to_string_lossy().into_owned();
    let velopack_path = path::velopack();
    let version = super::version();
    let channel = arch::velopack_channel();

    std::fs::create_dir_all(&velopack_path).unwrap();

    let main_exe = if consts::OS == "windows" { "Froststrap.exe" } else { "Froststrap" };

    let mut args: Vec<String> = [
        "pack",
        "--packId", "Froststrap",
        "--packTitle", "Froststrap",
        "--packAuthors", "Froststrap",
        "--packVersion", &version,
        "--packDir", &pack_path,
        "--outputDir", &velopack_path.to_string_lossy(),
        "--mainExe", main_exe,
        "--channel", &channel,
    ]
    .into_iter()
    .map(String::from)
    .collect();

    #[cfg(target_os = "windows")]
    {
        args.extend([
            "--icon".to_string(),
            path::git_root().join("Froststrap/Froststrap.ico").to_string_lossy().into_owned(),
            "--framework".to_string(),
            "net10.0-x64-runtime,vcredist143-x64".to_string(),
            "--msi".to_string(),
        ]);
    }

    #[cfg(target_os = "macos")]
    {
        args.extend(["--bundleId".to_string(), "xyz.froststrap.desktop".to_string()]);

        if env::var("SIGN").is_ok_and(|v| v.eq_ignore_ascii_case("true")) {
            let Some(entitlements) = entitlements else {
                log::error!("no entitlements provided for signing");
                std::process::exit(1);
            };

            args.extend([
                "--signAppIdentity".to_string(),
                required_env("DEVELOPER_ID_APP"),
                "--signInstallIdentity".to_string(),
                required_env("DEVELOPER_ID_INSTALLER"),
                "--signEntitlements".to_string(),
                entitlements.to_string_lossy().into_owned(),
                "--notaryProfile".to_string(),
                "froststrap-notary".to_string(),
            ]);
        }
    }

    #[cfg(target_os = "linux")]
    {
        args.extend([
            "--icon".to_string(),
            path::xtask_root().join("icon512.png").to_string_lossy().into_owned(),
        ]);
    }

    log::info!("Packing with vpk on channel {}", channel);
    let vpk_bin = vpk();
    process(&vpk_bin, args).exec_checked(&vpk_bin);
}
