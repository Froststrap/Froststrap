//! publish module

mod velopack;

use std::{fs, env, sync::OnceLock};
use crate::integrate::git::git;
use crate::{arch, args, path, utils::{log, process}};

#[cfg(target_os = "linux")]
#[path = "linux.rs"]
mod os_specific;

#[cfg(target_os = "macos")]
#[path = "macos.rs"]
mod os_specific;

#[cfg(target_os = "windows")]
#[path = "win.rs"]
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

fn env_nonempty(key: &str) -> Option<String> {
    env::var(key).ok().filter(|s| !s.trim().is_empty())
}

fn version() -> String {
    static VERSION: OnceLock<String> = OnceLock::new();
    VERSION.get_or_init(version_for_run).clone()
}

fn version_for_run() -> String {
    const DEFAULT: &str = "v0.0.2";

    let tag = if env::var("GITHUB_REF_TYPE").as_deref() == Ok("tag") {
        env_nonempty("GITHUB_REF_NAME").unwrap_or_default()
    } else {
        let mut tag = git(&["describe", "--tags", "--abbrev=0"])
            .unwrap_or_else(|| DEFAULT.to_string());

        if let Some(run) = env_nonempty("GITHUB_RUN_NUMBER") {
            tag = format!("{tag}-ci.{run}");
        }
        tag
    };

    let tag = if tag.trim().is_empty() {
        DEFAULT
    } else {
        tag.as_str()
    };
    tag.strip_prefix('v').unwrap_or(tag).to_string()
}

pub fn main() {
    super::restore::main();
    let args = args::get();

    let path: String = path::git_root()
        .join("Froststrap/Froststrap.csproj")
        .into_string()
        .unwrap();
    log::info!("Froststrap path: {}", path);
    let config = args.configuration;
    let output_path = path::dotnet_publish_artifacts().into_string().unwrap();
    let rust_target_triple = arch::rust_triple();
    let rid = arch::dotnet_rid();
    
    log::info!("Executing Publish");
    log::info!("Publishing {} to {}...", path, output_path);
    log::info!("AppVersion: {}", version());

    let args = [
        "publish".to_string(),
        path,
        "-c".to_string(),
        config.to_string(),
        "-r".to_string(),
        rid.to_string(),
        "-o".to_string(),
        output_path,
        format!("-p:RustTargetTriple={rust_target_triple}"),
        format!("-p:AppVersion={}", version()),
        "-nologo".to_string(),
    ];

    std::fs::create_dir_all(path::dotnet_publish_artifacts()).unwrap();
    path::write_output_gitignore();
    process("dotnet", args).exec_checked("dotnet publish");

    delete_pdbs(&path::dotnet_publish_artifacts()).unwrap();

    os_specific::main();

    log::info!("Build complete!");
}
