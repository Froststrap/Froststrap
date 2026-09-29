// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

pub mod cs;
pub mod registration;

#[unsafe(no_mangle)]
pub extern "C" fn init() -> i32 {
    registration::register()
}
