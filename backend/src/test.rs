#[cfg(test)]
mod test {
    use crate::{send_notification, send_notification_message, set_application};
    use std::ffi::CString;
    use std::sync::Once;

    static APP_INIT: Once = Once::new();

    fn ensure_app_set() {
        APP_INIT.call_once(|| {
            let _ = unsafe { set_application() };
        });
    }

    #[test]
    fn test_notification_send() {
        ensure_app_set();

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

        let result = unsafe {
            send_notification(
                title.as_ptr(),
                description.as_ptr(),
                png_bytes.as_ptr(),
                png_bytes.len(),
            )
        };
        assert_eq!(result, 0)
    }

    #[test]
    fn test_notification_message_send() {
        ensure_app_set();

        let title = CString::new("Test Title").unwrap();
        let description = CString::new("Testing description").unwrap();

        let result = unsafe { send_notification_message(title.as_ptr(), description.as_ptr(), 5) };

        assert_eq!(result, 0)
    }
}
