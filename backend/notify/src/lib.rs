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
use std::time::Duration;
use util::IStr;

logger::export_set_logger!();

#[unsafe(no_mangle)]
pub extern "C" fn init(bundle_id: *const c_char) -> i32 {
    let bundle_id = IStr::from(bundle_id).into();
    os::init(bundle_id)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn send_notification_message(
    title: *const c_char,
    description: *const c_char,
    dur: i32,
) -> i32 {
    let title = IStr::from(title).into();
    let description = IStr::from(description).into();
    let r = os::send_notification(title, description, Duration::from_secs(dur as _));

    log::debug!("Send notify result={r}");
    r
}
