// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use crate::data_types::{NotificationPermissionResult, SendNotificationResult};
use block2::RcBlock;
use objc2::rc::{Allocated, Retained};
use objc2::runtime::{AnyClass, AnyObject, Bool};
use objc2::{class, msg_send};
use std::ffi::{c_char, c_int, c_void};
use std::sync::{Arc, Condvar, Mutex, Once};
use std::time::Duration;

unsafe extern "C" {
    fn dlopen(path: *const c_char, mode: c_int) -> *mut c_void;
}

const RTLD_LAZY: c_int = 0x1;

const UN_OPT_BADGE: usize = 1 << 0;
const UN_OPT_SOUND: usize = 1 << 1;
const UN_OPT_ALERT: usize = 1 << 2;

const UN_STATUS_AUTHORIZED: isize = 2;
const UN_STATUS_PROVISIONAL: isize = 3;

const NS_UTF8_STRING_ENCODING: usize = 4;

type Slot<T> = Arc<(Mutex<Option<T>>, Condvar)>;

fn new_slot<T>() -> Slot<T> {
    Arc::new((Mutex::new(None), Condvar::new()))
}

fn fulfill<T>(slot: &Slot<T>, value: T) {
    let (lock, cvar) = &**slot;
    *lock.lock().unwrap() = Some(value);
    cvar.notify_one();
}

fn wait_for<T: Copy>(slot: &Slot<T>, secs: u64) -> Option<T> {
    let (lock, cvar) = &**slot;
    let guard = lock.lock().unwrap();
    let (guard, _) = cvar
        .wait_timeout_while(guard, Duration::from_secs(secs), |r| r.is_none())
        .unwrap();
    *guard
}

fn ensure_frameworks() {
    static ONCE: Once = Once::new();
    ONCE.call_once(|| unsafe {
        dlopen(
            c"/System/Library/Frameworks/Foundation.framework/Foundation".as_ptr(),
            RTLD_LAZY,
        );
        dlopen(
            c"/System/Library/Frameworks/UserNotifications.framework/UserNotifications".as_ptr(),
            RTLD_LAZY,
        );
    });
}

fn ns_string(s: &str) -> Retained<AnyObject> {
    unsafe {
        let alloc: Allocated<AnyObject> = msg_send![class!(NSString), alloc];
        msg_send![
            alloc,
            initWithBytes: s.as_ptr() as *const c_void,
            length: s.len(),
            encoding: NS_UTF8_STRING_ENCODING,
        ]
    }
}

fn notification_center() -> Option<Retained<AnyObject>> {
    ensure_frameworks();
    let cls = AnyClass::get(c"UNUserNotificationCenter")?;
    unsafe { msg_send![cls, currentNotificationCenter] }
}

fn has_valid_bundle_context() -> bool {
    ensure_frameworks();
    unsafe {
        let bundle: Retained<AnyObject> = msg_send![class!(NSBundle), mainBundle];
        let id: Option<Retained<AnyObject>> = msg_send![&*bundle, bundleIdentifier];
        id.is_some()
    }
}

pub fn request_notification_permission() -> i32 {
    if !has_valid_bundle_context() {
        return NotificationPermissionResult::NoBundleContext as i32;
    }

    let Some(center) = notification_center() else {
        return NotificationPermissionResult::NoBundleContext as i32;
    };
    let options = UN_OPT_ALERT | UN_OPT_SOUND | UN_OPT_BADGE;

    let slot = new_slot::<bool>();
    let slot_for_block = Arc::clone(&slot);

    let handler = RcBlock::new(move |granted: Bool, _error: *mut AnyObject| {
        fulfill(&slot_for_block, granted.as_bool());
    });

    unsafe {
        let _: () = msg_send![
            &*center,
            requestAuthorizationWithOptions: options,
            completionHandler: &*handler,
        ];
    }

    match wait_for(&slot, 30) {
        Some(true) => NotificationPermissionResult::Granted as i32,
        Some(false) => NotificationPermissionResult::Denied as i32,
        None => NotificationPermissionResult::TimedOut as i32,
    }
}

fn current_authorization_status(center: &AnyObject) -> Option<isize> {
    let slot = new_slot::<isize>();
    let slot_for_block = Arc::clone(&slot);

    let handler = RcBlock::new(move |settings: *mut AnyObject| {
        if settings.is_null() {
            return;
        }
        let status: isize = unsafe { msg_send![&*settings, authorizationStatus] };
        fulfill(&slot_for_block, status);
    });

    unsafe {
        let _: () = msg_send![center, getNotificationSettingsWithCompletionHandler: &*handler];
    }

    wait_for(&slot, 5)
}

#[unsafe(no_mangle)]
pub fn send_notification(title: String, body: String) -> i32 {
    if !has_valid_bundle_context() {
        return SendNotificationResult::NoBundleContext as i32;
    }

    let Some(center) = notification_center() else {
        return SendNotificationResult::NoBundleContext as i32;
    };

    match current_authorization_status(&center) {
        Some(UN_STATUS_AUTHORIZED) | Some(UN_STATUS_PROVISIONAL) => {}
        Some(_) => return SendNotificationResult::NotAuthorized as i32,
        None => return SendNotificationResult::TimedOut as i32,
    }

    let (Some(content_cls), Some(request_cls)) = (
        AnyClass::get(c"UNMutableNotificationContent"),
        AnyClass::get(c"UNNotificationRequest"),
    ) else {
        return SendNotificationResult::OsError as i32;
    };

    let request: Retained<AnyObject> = unsafe {
        let content: Retained<AnyObject> = msg_send![content_cls, new];
        let _: () = msg_send![&*content, setTitle: &*ns_string(&title)];
        let _: () = msg_send![&*content, setBody: &*ns_string(&body)];

        let uuid: Retained<AnyObject> = msg_send![class!(NSUUID), UUID];
        let identifier: Retained<AnyObject> = msg_send![&*uuid, UUIDString];

        msg_send![
            request_cls,
            requestWithIdentifier: &*identifier,
            content: &*content,
            trigger: None::<&AnyObject>,
        ]
    };

    let slot = new_slot::<bool>();
    let slot_for_block = Arc::clone(&slot);

    let completion = RcBlock::new(move |error: *mut AnyObject| {
        fulfill(&slot_for_block, error.is_null());
    });

    unsafe {
        let _: () = msg_send![
            &*center,
            addNotificationRequest: &*request,
            withCompletionHandler: &*completion,
        ];
    }

    match wait_for(&slot, 5) {
        Some(true) => SendNotificationResult::Sent as i32,
        Some(false) => SendNotificationResult::OsError as i32,
        None => SendNotificationResult::TimedOut as i32,
    }
}
