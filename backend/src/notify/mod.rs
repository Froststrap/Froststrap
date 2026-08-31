#[cfg(target_os = "macos")]
pub mod macos;
#[cfg(target_os = "windows")]
pub mod win;

use std::{ffi::CStr, os::raw::c_char};

use crate::notify::macos::SendNotificationResult;

/// macOS only- No-op on other platforms.
/// Will still execute on other systems to provide
/// a simple ABI, and it's usage.
#[unsafe(no_mangle)]
pub fn request_notificaiton_permission() -> i32 {
    if cfg!(target_os = "macos") {
        macos::request_notification_permission()
    } else {
        0
    }
}

#[unsafe(no_mangle)]
#[cfg(target_os = "windows")]
pub fn set_application(b: *const c_char) -> i32 {
    win::set_application(b)
}

#[unsafe(no_mangle)]
#[cfg(not(target_os = "windows"))]
pub fn set_application(_b: *const c_char) -> i32 {
    0
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn send_notification_message(
    title: *const c_char,
    description: *const c_char,
    duration: i32,
) -> i32 {
    let _ = duration;

    let Some(title) = (unsafe { c_str_to_string(title) }) else {
        return SendNotificationResult::InvalidUtf8 as i32;
    };
    let Some(description) = (unsafe { c_str_to_string(description) }) else {
        return SendNotificationResult::InvalidUtf8 as i32;
    };

    macos::send_notification(title, description)
}

unsafe fn c_str_to_string(ptr: *const c_char) -> Option<String> {
    if ptr.is_null() {
        return None;
    }
    unsafe { CStr::from_ptr(ptr) }
        .to_str()
        .ok()
        .map(str::to_owned)
}
