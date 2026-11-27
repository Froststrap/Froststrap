// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

//! publish module

use std::{fs, env, sync::OnceLock};
use crate::integrate::velopack;
use crate::{arch, args, path, utils::{log, process}};

#[cfg(target_os = "linux")]
#[path = "linux.rs"]
mod os_specific;

#[cfg(target_os = "macos")]
#[path = "macos.rs"]
mod os_specific;

fn delete_pdbs(dir: &std::path::Path) -> std::io::Result<()> {
    for entry in fs::read_dir(dir)? {
        let path = entry?.path();
        if path.is_file() && path.extension().is_some_and(|e| e == "pdb") {
            log::info!("Deleting debug file {}...", path.display());
            fs::remove_file(&path)?;
        }
    }
    Ok(())
}

fn version() -> String {
    static VERSION: OnceLock<String> = OnceLock::new();
    VERSION.get_or_init(version_for_run).clone()
}

fn version_for_run() -> String {
    let csproj = path::git_root().join("Froststrap/Froststrap.csproj");
    let contents = fs::read_to_string(&csproj).expect("failed to read Froststrap.csproj");

    let version = contents
        .lines()
        .find_map(|line| {
            let line = line.trim();
            line.starts_with("<AppVersion")
                .then(|| line.split_once('>')?.1.split_once("</AppVersion>").map(|(v, _)| v.trim().to_string()))
                .flatten()
        })
        .filter(|v| !v.is_empty())
        .expect("Froststrap.csproj has no <AppVersion>");

    match env::var("GITHUB_RUN_NUMBER") {
        Ok(run) if !run.is_empty() && env::var("GITHUB_REF_TYPE").as_deref() != Ok("tag") => {
            format!("{version}-ci.{run}")
        }
        _ => version,
    }
}

pub fn main() {
    super::restore::main();
    let a = args::get();

    let path: String = path::git_root()
        .join("Froststrap/Froststrap.csproj")
        .into_string()
        .unwrap();
    log::info!("Froststrap path: {}", path);
    let config = a.configuration;
    let output_path = path::dotnet_publish_artifacts().into_string().unwrap();
    let rust_target_triple = arch::rust_triple();
    let rid = arch::dotnet_rid();
    
    log::info!("Executing Publish");
    log::info!("Publishing {} to {}...", path, output_path);
    log::info!("AppVersion: {}", version());

    let args = [
        "publish",
        &path,
        "-c",
        &config.to_string(),
        "-r",
        &rid,
        "-o",
        &output_path,
        &format!("-p:RustTargetTriple={rust_target_triple}"),
        &format!("-p:AppVersion={}", version()),
        "-nologo",
    ];

    std::fs::create_dir_all(path::dotnet_publish_artifacts()).unwrap();
    path::write_output_gitignore();
    process("dotnet", args).exec_checked("dotnet publish");

    delete_pdbs(&path::dotnet_publish_artifacts()).unwrap();

    #[cfg(not(any(target_os = "windows", target_os = "macos")))]
    os_specific::main();
    #[cfg(target_os = "macos")]
    let (app_path, entitlements_path) = os_specific::main();

    if !a.no_installers {
        #[cfg(target_os = "macos")]
        velopack::pack(version(), &app_path, Some(&entitlements_path));
        #[cfg(not(target_os = "macos"))]
        velopack::pack(version(), &path::dotnet_publish_artifacts(), None);
    }

    log::info!("Build complete!");
}
