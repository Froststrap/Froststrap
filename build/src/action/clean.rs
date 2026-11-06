//! clean module

use crate::{path, utils::log};

pub fn main() {
    log::info!("Executing Clean");

    let orfh = path::output_root();

    if orfh.exists() {
        match std::fs::remove_dir_all(orfh.clone()) {
            Ok(_) => log::info!("Deleted {}", orfh.to_string_lossy()),
            Err(e) => {
                log::error!("Couldn't delete {} due to {}", orfh.to_string_lossy(), e);
                std::process::exit(1);
            }
        }
    } else {
        log::info!("{} doesn't exist", orfh.to_string_lossy())
    }
}
