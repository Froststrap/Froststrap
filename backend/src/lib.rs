use std::ffi::CStr;
use std::os::raw::c_char;

use image::DynamicImage;
use notify_rust::Notification;

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

    let result = match notification.show() {
        Ok(_) => 0,
        Err(_) => -3,
    };

    let _ = std::fs::remove_file(&temp_path);
    result
}
