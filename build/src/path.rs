//! Helpers for path finding

use std::path::{Path, PathBuf};
use std::sync::OnceLock;

const XTASK_DIR: &str = env!("CARGO_MANIFEST_DIR");

pub fn git_root() -> &'static Path {
    static ROOT: OnceLock<PathBuf> = OnceLock::new();
    ROOT.get_or_init(discover_root)
}

fn discover_root() -> PathBuf {
    if let Ok(out) = std::process::Command::new("git")
        .args(["rev-parse", "--show-toplevel"])
        .output()
        && out.status.success()
    {
        let root = String::from_utf8_lossy(&out.stdout).trim().to_string();
        if !root.is_empty() {
            return PathBuf::from(root);
        }
    }

    std::env::current_dir().unwrap_or_else(|_| PathBuf::from("."))
}

pub fn xtask_root() -> &'static Path {
    static ROOT: OnceLock<PathBuf> = OnceLock::new();
    ROOT.get_or_init(|| {
        let checkout = git_root().join("build");
        if checkout.join("Cargo.toml").exists() {
            checkout
        } else {
            PathBuf::from(XTASK_DIR)
        }
    })
}

pub fn output_root() -> PathBuf {
    git_root().join(".build")
}

pub fn write_output_gitignore() {
    std::fs::write(output_root().join(".gitignore"), "*").unwrap();
}

pub fn dotnet_publish_artifacts() -> PathBuf {
    output_root().join("publish")
}
pub fn dotnet_build_artifacts() -> PathBuf {
    output_root().join("build")
}
pub fn bundling_artifacts() -> PathBuf {
    output_root().join("bundling")
}
pub fn dist() -> PathBuf {
    output_root().join("dist")
}
pub fn velopack() -> PathBuf {
    output_root().join("velopack")
}
