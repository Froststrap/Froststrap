//! restore module

use crate::utils::{log, process};

pub fn main() {
    log::info!("Executing Restore");

    process("dotnet", ["restore"]).exec_checked("dotnet restore");
}
