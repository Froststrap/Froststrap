// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

pub mod util;
pub mod test;
pub mod data_types;

#[cfg(target_os = "linux")]
pub mod linux;
#[cfg(target_os = "macos")]
pub mod macos;
#[cfg(target_os = "windows")]
pub mod win;

use std::os::raw::c_char;
use util::IStr;

/// Runtime initialzer, will No-op on Linux systems
/// 
/// On macOS it will request notification permission.
///
/// On Windows it will set the thread AUMID.
#[unsafe(no_mangle)]
pub extern "C" fn init() -> i32 {
    #[cfg(target_os = "macos")]
    return macos::request_notification_permission();
    
    #[cfg(target_os = "windows")]
    return win::set_application("xyz.froststrap.desktop".into());

    #[cfg(target_os = "linux")]
    return 0;
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

    #[cfg(target_os = "macos")]
    {
        let r = macos::send_notification(title, description);
        println!("Send notify result={r}");
        r
    }

    #[cfg(target_os = "linux")]
    return linux::send_notification(title, description);

    #[cfg(target_os = "windows")]
    return win::send_notification(title, description);
}