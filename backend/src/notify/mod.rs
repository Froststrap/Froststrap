pub mod data_types;
#[cfg(target_os = "linux")]
pub mod linux;
#[cfg(target_os = "macos")]
pub mod macos;
#[cfg(target_os = "windows")]
pub mod win;

use crate::notify::data_types::SendNotificationResult;
use std::{ffi::CStr, os::raw::c_char};

/// macOS only- No-op on other platforms.
/// Will still execute on other systems to provide
/// a simple ABI, and it's usage.
#[unsafe(no_mangle)]
pub fn request_notificaiton_permission() -> i32 {
    #[cfg(target_os = "macos")]
    return macos::request_notification_permission();

    #[cfg(not(target_os = "macos"))]
    return 0;
}

#[allow(unused)]
#[unsafe(no_mangle)]
pub fn set_application(b: *const c_char) -> i32 {
    #[cfg(target_os = "windows")]
    {
        use data_types::SetApplicationResult;
        let Some(bundle_ident) = (unsafe { c_str_to_string(b) }) else {
            return SetApplicationResult::InvalidUtf8 as i32;
        };
        return win::set_application(bundle_ident);
    }

    #[cfg(not(target_os = "windows"))]
    return 0;
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

    #[cfg(target_os = "macos")]
    return macos::send_notification(title, description);

    #[cfg(target_os = "linux")]
    return linux::send_notification(title, description);

    #[cfg(target_os = "windows")]
    return win::send_notification(title, description);
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
