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
        crate::notify::set_application(CString::new("xyz.froststrap.desktop").unwrap().as_ptr());
        let title = CString::new("Notification Test").unwrap();
        let description = CString::new("A description came with the test too!").unwrap();

        let result = unsafe { send_notification_message(title.as_ptr(), description.as_ptr(), 5) };

        assert_eq!(result, 0)
    }
}
