// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

#[cfg(test)]
mod test {
    use crate::{send_notification_message, init};
    use std::ffi::CString;

    #[test]
    fn test_init() {
        assert_eq!(init(), 0)
    }

    #[test]
    fn test_notification_send() {
        init();
        let title = CString::new("Notification Test").unwrap();
        let description = CString::new("A description came with the test too!").unwrap();

        let result = unsafe { send_notification_message(title.as_ptr(), description.as_ptr(), 5) };

        assert_eq!(result, 0)
    }
}
