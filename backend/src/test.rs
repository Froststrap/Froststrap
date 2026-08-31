#[cfg(test)]
mod test {
    use crate::notify::{request_notificaiton_permission, send_notification_message};
    use std::ffi::CString;
    use std::sync::Once;

    static APP_INIT: Once = Once::new();

    fn ensure_app_set() {
        // APP_INIT.call_once(|| {
        //     let _ = unsafe { set_application() };
        // });
    }

    #[test]
    fn test_notification_request() {
        assert_eq!(request_notificaiton_permission(), 0)
    }

    #[test]
    fn test_notification_send() {
        ensure_app_set();

        let title = CString::new("Test Title").unwrap();
        let description = CString::new("Testing description").unwrap();

        let result = unsafe { send_notification_message(title.as_ptr(), description.as_ptr(), 5) };

        assert_eq!(result, 0)
    }
}
