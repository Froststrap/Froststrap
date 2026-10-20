//! os specific stuff for publish

use std::{fs, path::PathBuf};
use pissing::prelude::*;
use crate::{
    arch, args,
    args::Configuration,
    bundle::macos::AppBuilder,
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

fn info_plist(version: &str) -> PListFile {
    PListFile::new("1.0").content(dict![
        string("CFBundleDevelopmentRegion", "en"),
        string("CFBundleExecutable", "Froststrap"),
        string("CFBundleIdentifier", "xyz.froststrap.desktop"),
        string("CFBundleName", "Froststrap"),
        string("CFBundleDisplayName", "Froststrap"),
        string("CFBundlePackageType", "APPL"),
        string("CFBundleInfoDictionaryVersion", "6.0"),
        string("CFBundleIconFile", "AppIcon"),
        string("CFBundleIconName", "AppIcon"),
        string("CFBundleShortVersionString", version),
        string("CFBundleVersion", version.replace('.', "")),
        string("LSMinimumSystemVersion", "12.0"),
        string("LSApplicationCategoryType", "public.app-category.utilities"),
        entry("CFBundleSupportedPlatforms", array!["MacOSX"]),
        array("CFBundleURLTypes", [dict![
            string("CFBundleURLName", "xyz.froststrap.roblox"),
            string("CFBundleTypeRole", "Viewer"),
            entry("CFBundleURLSchemes", array![
                "roblox",
                "roblox-player",
                "roblox-studio",
                "roblox-studio-auth",
            ]),
        ]]),
        array("UTExportedTypeDeclarations", [
            dict![
                string("UTTypeIdentifier", "xyz.froststrap.roblox.place"),
                string("UTTypeDescription", "Roblox Place"),
                entry("UTTypeConformsTo", array!["public.data"]),
                dict("UTTypeTagSpecification", [
                    entry("public.filename-extension", array!["rbxl", "rbxlx"]),
                ]),
            ],
            dict![
                string("UTTypeIdentifier", "xyz.froststrap.roblox.model"),
                string("UTTypeDescription", "Roblox Model"),
                entry("UTTypeConformsTo", array!["public.data"]),
                dict("UTTypeTagSpecification", [
                    entry("public.filename-extension", array!["rbxm", "rbxmx"]),
                ]),
            ],
        ]),
        array("CFBundleDocumentTypes", [
            dict![
                string("CFBundleTypeName", "Roblox Place"),
                string("CFBundleTypeRole", "Viewer"),
                entry("LSItemContentTypes", array!["xyz.froststrap.roblox.place"]),
            ],
            dict![
                string("CFBundleTypeName", "Roblox Model"),
                string("CFBundleTypeRole", "Viewer"),
                entry("LSItemContentTypes", array!["xyz.froststrap.roblox.model"]),
            ],
        ]),
    ])
}

fn entitlements() -> PListFile {
    PListFile::new("1.0").content(dict![
        bool("com.apple.security.cs.allow-jit", true),
        bool("com.apple.security.cs.allow-unsigned-executable-memory", true),
        bool("com.apple.security.cs.disable-library-validation", true),
        bool("com.apple.security.files.downloads.read-write", false),
        bool("com.apple.security.files.user-selected.read-write", true),
        bool("com.apple.security.network.client", true),
    ])
}

pub fn main() {
    let a = args::get();
    let version = super::version();

    let publish_dir = path::dotnet_publish_artifacts();
    let dist = path::dist();
    fs::create_dir_all(&dist).unwrap();

    let main_executable = publish_dir.join("Froststrap");
    let icon_path = path::xtask_root().join("macicon512.png");

    let entitlements_path = path::bundling_artifacts().join("Froststrap.entitlements");
    fs::create_dir_all(path::bundling_artifacts()).expect("Failed to create bundling directory");
    entitlements()
        .write(&entitlements_path)
        .expect("Failed to write Froststrap.entitlements");

    let mut builder = AppBuilder::new("Froststrap")
        .info_plist(info_plist(&version))
        .app_icon(icon_path)
        .binary(&main_executable);

    for lib in ["libvirtualdisplay.dylib", "libmobileappreg.dylib", "libnotify.dylib"] {
        let dylib_src = find_dylib(lib);
        builder = builder.framework(dylib_src);
    }

    let app_path = builder.build(&dist);

    if a.no_installers {
        return;
    }
    super::velopack::pack(&app_path, Some(&entitlements_path));
}
