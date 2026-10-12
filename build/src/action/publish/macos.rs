//! os specific stuff for publish

use std::{fs, path::PathBuf};
use crate::{
    arch, args,
    args::Configuration,
    path,
    utils::{log, process},
};

fn find_dylib(file_name: &str) -> PathBuf {
    let profile = match args::get().configuration {
        Configuration::Release => "release",
        Configuration::Debug => "debug",
    };
    let triple = arch::rust_triple();
    let p = path::git_root()
        .join("backend/target")
        .join(triple)
        .join(profile)
        .join(file_name);

    if !p.is_file() {
        log::error!(
            "{} not found - did `cargo build` run for the {} profile on {}?",
            p.display(), profile, triple
        );
        std::process::exit(1);
    }
    p
}

pub fn main() {
    let a = args::get();
    let version = super::version();

    let mac_app = path::xtask_root().join("macApp");
    let xcode_project = mac_app.join("macApp.xcodeproj");
    let publish_dir = path::dotnet_publish_artifacts();

    for lib in ["libvirtualdisplay.dylib", "libmobileappreg.dylib", "libnotify.dylib"] {
        let src = find_dylib(lib);
        log::info!("Copying {} into {}", src.display(), publish_dir.display());
        fs::copy(&src, publish_dir.join(lib)).unwrap();
    }

    log::info!("Building {} with xcodebuild", xcode_project.display());

    let xcode_arch = match arch::normalized_arch() {
        "x86_64" => "x86_64",
        _ => "arm64",
    };
    let config = match a.configuration {
        Configuration::Debug => "Debug",
        Configuration::Release => "Release",
    };

    process(
        "xcodebuild",
        [
            "-project".to_string(),
            xcode_project.to_string_lossy().into_owned(),
            "-target".to_string(),
            "Froststrap".to_string(),
            "-configuration".to_string(),
            config.to_string(),
            format!("MARKETING_VERSION={version}"),
            format!("CURRENT_PROJECT_VERSION={}", version.replace('.', "")),
            "CODE_SIGNING_ALLOWED=NO".to_string(),
            format!("ARCHS={xcode_arch}"),
            "ONLY_ACTIVE_ARCH=NO".to_string(),
            "build".to_string(),
        ],
    )
    .exec_checked("xcodebuild");

    let dist = path::dist();
    fs::create_dir_all(&dist).unwrap();

    let src = mac_app.join("build").join(config).join("Froststrap.app");
    let dest = dist.join("Froststrap.app");
    log::info!("Copying {} artifact to {}", src.display(), dest.display());

    if dest.is_dir() {
        fs::remove_dir_all(&dest).unwrap();
    }
    process("ditto", [&src, &dest]).exec_checked("ditto");

    if a.no_installers {
        return;
    }
    super::velopack::pack(&dest);
}

