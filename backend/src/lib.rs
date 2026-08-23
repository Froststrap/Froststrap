mod test;

use std::ffi::CStr;
use std::os::raw::c_char;

use image::DynamicImage;
use notify_rust::Notification;

#[cfg(not(target_os = "linux"))]
const APP_ID: &'static str = "xyz.froststrap.desktop";
#[cfg(target_os = "linux")]
const APP_ID: &'static str = "Froststrap";

#[unsafe(no_mangle)]
pub unsafe extern "C" fn send_notification(
    title: *const c_char,
    description: *const c_char,
    image_data: *const u8,
    image_len: usize,
) -> i32 {
    let title = unsafe { CStr::from_ptr(title) }.to_string_lossy();
    let description = unsafe { CStr::from_ptr(description) }.to_string_lossy();
    let buffer = unsafe { std::slice::from_raw_parts(image_data, image_len) };

    #[cfg(target_os = "macos")]
    match notify_rust::set_application(APP_ID) {
        Ok(_) => (),
        Err(_) => return -4,
    };

    let dynamic_image: DynamicImage = match image::load_from_memory(buffer) {
        Ok(img) => img,
        Err(e) => {
            eprintln!("image decode failed: {e:?}");
            return -1;
        }
    };

    let temp_path =
        std::env::temp_dir().join(format!("froststrap-notif-{}.png", std::process::id()));
    if let Err(_) = dynamic_image.save(&temp_path) {
        return -2;
    }

    let mut notification = Notification::new();
    notification.summary(&title).body(&description);
    notification.image_path(temp_path.to_string_lossy().as_ref());

    #[cfg(target_os = "windows")]
    notification.app_id(APP_ID);

    #[cfg(target_os = "linux")]
    notification.appname(APP_ID);

    let result = match notification.show() {
        Ok(_) => 0,
        Err(_) => -3,
    };

    let _ = std::fs::remove_file(&temp_path);
    result
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn send_notification_message(
    title: *const c_char,
    description: *const c_char,
    duration: i32,
) -> i32 {
    let title = unsafe { CStr::from_ptr(title) }.to_string_lossy();
    let description = unsafe { CStr::from_ptr(description) }.to_string_lossy();

    #[cfg(target_os = "macos")]
    match notify_rust::set_application(APP_ID) {
        Ok(_) => (),
        Err(_) => return -4,
    };

    let mut notification = Notification::new();
    notification
        .summary(&title)
        .body(&description)
        .timeout(duration);

    #[cfg(target_os = "windows")]
    notification.app_id(APP_ID);

    #[cfg(target_os = "linux")]
    notification.appname(APP_ID);

    let result = match notification.show() {
        Ok(_) => 0,
        Err(_) => -3,
    };

    result
}
