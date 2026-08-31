#[cfg(test)]
mod test {
    use crate::notify::{request_notificaiton_permission, send_notification_message};
    use std::ffi::CString;

    #[test]
    fn test_notification_request() {
        assert_eq!(request_notificaiton_permission(), 0)
    }

    #[test]
    fn test_notification_send() {
        let title = CString::new("Test Title").unwrap();
        let description = CString::new("Testing description").unwrap();

        let result = unsafe { send_notification_message(title.as_ptr(), description.as_ptr(), 5) };

        assert_eq!(result, 0)
    }
}
