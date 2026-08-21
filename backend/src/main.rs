//! This is a test file full of shit testing libs functionality

use rbackend::{send_notification, send_notification_message};
use std::ffi::CString;

fn main() {
    test_notification_send();
    test_notification_message_send();
}

fn test_notification_send() {
    let img = image::RgbImage::from_pixel(1, 1, image::Rgb([255, 0, 0]));
    let mut png_bytes = Vec::new();
    image::DynamicImage::ImageRgb8(img)
        .write_to(
            &mut std::io::Cursor::new(&mut png_bytes),
            image::ImageFormat::Png,
        )
        .unwrap();

    let title = CString::new("Test Notification").unwrap();
    let description = CString::new("Fired from a Rust test").unwrap();

    unsafe {
        send_notification(
            title.as_ptr(),
            description.as_ptr(),
            png_bytes.as_ptr(),
            png_bytes.len(),
        )
    };
}

fn test_notification_message_send() {
    let title = CString::new("Test Title").unwrap();
    let description = CString::new("Testing description").unwrap();

    unsafe { send_notification_message(title.as_ptr(), description.as_ptr(), 5) };
}
