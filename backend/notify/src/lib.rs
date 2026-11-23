// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

pub mod data_types;
pub mod test;
pub mod util;

#[cfg(target_os = "linux")]
#[path = "linux/mod.rs"]
pub mod os;
#[cfg(target_os = "macos")]
#[path = "macos/mod.rs"]
pub mod os;
#[cfg(target_os = "windows")]
#[path = "win.rs"]
pub mod os;

use std::os::raw::c_char;
use util::IStr;

#[cfg(not(target_os = "windows"))]
use std::time::Duration;

logger::export_set_logger!();

/// Runtime initializer, will No-op on Linux systems
///
/// On macOS it will request notification permission.
///
/// On Windows it will set the thread AUMID.
#[unsafe(no_mangle)]
pub extern "C" fn init(bundle_id: *const c_char) -> i32 {
    let bundle_id = IStr::from(bundle_id).into();
    return os::init(bundle_id);
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn send_notification_message(
    title: *const c_char,
    description: *const c_char,
    duration: i32,
) -> i32 {
    _ = duration;

    let title = IStr::from(title).into();
    let description = IStr::from(description).into();
    let r = os::send_notification(title, description, Duration::from_secs(2));

    log::debug!("Send notify result={r}");
    r
}
