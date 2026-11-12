//! clean module

use crate::{path, utils::log};

pub fn main() {
    log::info!("Executing Clean");

    let orfh = path::output_root();

    if orfh.exists() {
        match std::fs::remove_dir_all(&orfh) {
            Ok(_) => log::info!("Deleted {}", orfh.to_string_lossy()),
            Err(e) => {
                log::error!("Couldn't delete {} due to {}", orfh.to_string_lossy(), e);
                std::process::exit(1);
            }
        }
    }

    let root = match std::env::current_dir() {
        Ok(dir) => dir,
        Err(e) => {
            log::error!("Couldn't get current directory: {}", e);
            std::process::exit(1);
        }
    };

    clean_directories(&root);
}

fn clean_directories(root: &std::path::Path) {
    let entries = match std::fs::read_dir(root) {
        Ok(entries) => entries,
        Err(e) => {
            log::error!("Couldn't read {} due to {}", root.display(), e);
            return;
        }
    };

    for entry in entries.flatten() {
        let dir = entry.path();

        if !dir.is_dir() {
            continue;
        }

        let name = dir.file_name().and_then(|n| n.to_str());

        if matches!(name, Some("bin" | "obj")) {
            match std::fs::remove_dir_all(&dir) {
                Ok(_) => log::info!("Deleted {}", dir.display()),
                Err(e) => {
                    log::error!("Couldn't delete {} due to {}", dir.display(), e);
                }
            }

            continue;
        }

        clean_directories(&dir);
    }
}
