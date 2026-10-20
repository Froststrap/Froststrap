//! Handling for getting and serializing Arch details
//! for different build system.

use std::env::consts;

use crate::utils::log;

pub fn target_arch() -> &'static str {
    let raw = crate::args::get()
        .override_arch
        .as_deref()
        .unwrap_or(consts::ARCH);
    dotnet_arch_name(normalize_arch(raw))
}

/// Always "x86_64" or "aarch64".
pub fn normalized_arch() -> &'static str {
    normalize_arch(target_arch())
}

pub fn rust_triple() -> &'static str {
    match (normalized_arch(), consts::OS) {
        ("x86_64", "macos") => "x86_64-apple-darwin",
        ("aarch64", "macos") => "aarch64-apple-darwin",
        ("x86_64", "windows") => "x86_64-pc-windows-msvc",
        ("aarch64", "windows") => "aarch64-pc-windows-msvc",
        ("x86_64", "linux") => "x86_64-unknown-linux-gnu",
        ("aarch64", "linux") => "aarch64-unknown-linux-gnu",
        _ => {
            log::error!(
                "Unsupported target architecture '{}' for the current operating system.",
                target_arch()
            );
            std::process::exit(1);
        }
    }
}

pub fn dotnet_rid() -> String {
    let os = match consts::OS {
        "linux" => "linux",
        "windows" => "win",
        "macos" => "osx",
        other => {
            log::error!("Unsupported operating system '{other}' for publishing.");
            std::process::exit(1);
        }
    };

    format!("{os}-{}", target_arch())
}

/// Arch spelling expected by nFPM in its `arch:` field.
pub fn nfpm_arch() -> &'static str {
    nfpm_arch_name(normalized_arch())
}

pub fn velopack_channel() -> String {
    let arch = target_arch();
    match (consts::OS, arch) {
        ("windows", "x64") => "windows-x64".to_string(),
        ("macos", a @ ("x64" | "arm64")) => format!("macos-{a}"),
        ("linux", "x64") => "linux-x64".to_string(),
        _ => {
            log::error!(
                "Velopack does not support channel for {} {arch}.",
                consts::OS
            );
            std::process::exit(1);
        }
    }
}

/// Maps the many spellings of an arch to a better name.
pub fn normalize_arch(arch: &str) -> &'static str {
    match arch {
        "x64" | "x86_64" | "x86-64" | "amd64" => "x86_64",
        "aarch64" | "arm64" => "aarch64",
        other => panic!("Unrecognized arch: {other}"),
    }
}

fn dotnet_arch_name(normalized: &str) -> &'static str {
    match normalized {
        "x86_64" => "x64",
        "aarch64" => "arm64",
        other => panic!("Unrecognized arch: {other}"),
    }
}

fn nfpm_arch_name(normalized: &str) -> &'static str {
    match normalized {
        "aarch64" => "arm64",
        _ => "amd64",
    }
}
