//! os specific stuff for publish

use std::{fs, path::PathBuf};
mod plist;
use crate::{
    arch, args,
    args::Configuration,
    bundle::macos::{AppBuilder},
    path,
    utils::log,
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

    let publish_dir = path::dotnet_publish_artifacts();
    let dist = path::dist();
    fs::create_dir_all(&dist).unwrap();

    let main_executable = publish_dir.join("Froststrap");
    let icon_path = path::xtask_root().join("macicon512.png");
    let info_plist = plist::build_info_plist(&version);

    let mut builder = AppBuilder::new("Froststrap")
        .info_plist(info_plist)
        .app_icon(icon_path)
        .binary(&main_executable);

    for lib in ["libvirtualdisplay.dylib", "libmobileappreg.dylib", "libnotify.dylib"] {
        let dylib_src = find_dylib(lib);
        builder = builder.framework(dylib_src);
    }

    let entitlements_path = path::xtask_root().join("src/action/publish/macos/Froststrap.entitlements");
    if entitlements_path.exists() {
        builder = builder.entitlements(&entitlements_path);
    }

    let app_path = builder.build(&dist);

    if a.no_installers {
        return;
    }
    super::velopack::pack(&app_path);
}
