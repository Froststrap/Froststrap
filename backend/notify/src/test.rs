// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

#[cfg(test)]
mod test {
    use crate::{init, send_notification_message};

    #[test]
    fn test_init() {
        assert_eq!(init(c"test".as_ptr()), 0)
    }

    #[test]
    fn test_notification_send() {
        init(c"test".as_ptr());
        let title = c"Notification Test";
        let description = c"A description came with the test too!";
        let result = unsafe { send_notification_message(title.as_ptr(), description.as_ptr(), 5) };

        assert_eq!(result, 0)
    }
}
