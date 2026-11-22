// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

//! restore module

use crate::utils::{log, process};

pub fn main() {
    log::info!("Executing Restore");

    let args = [
        "restore",
        "./Froststrap/Froststrap.csproj"
    ];

    process("dotnet", args).exec_checked("dotnet restore");
}
