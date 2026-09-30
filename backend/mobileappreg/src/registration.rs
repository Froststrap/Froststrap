// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

const BUNDLE_ID: &str = "xyz.froststrap.desktop";
pub const SCHEMES: [&str; 2] = ["roblox", "roblox-player"];

/// Forces a re-register with `CoreServices`
pub fn register() -> i32 {
    for s in SCHEMES {
        match crate::cs::set_default_handler_for_url_scheme(s, BUNDLE_ID) {
            Ok(()) => println!("Set {s} to {BUNDLE_ID}"),
            Err(code) => eprintln!("Failed to set {s} (OSStatus {code})"),
        }
    }
    // TODO: make this above an expression which returns number stuff like notify
    //       so it actually has error feedback
    0
}
