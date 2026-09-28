// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use objc2::rc::{Allocated, Retained};
use objc2::runtime::{AnyClass, AnyObject};
use objc2::{class, msg_send};
use std::ffi::{CStr, c_char, c_int, c_void};
use std::sync::Once;

unsafe extern "C" {
    fn dlopen(path: *const c_char, mode: c_int) -> *mut c_void;
}

const RTLD_LAZY: c_int = 0x1;
const NS_UTF8_STRING_ENCODING: usize = 4;

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

pub(super) fn class(name: &CStr) -> Option<&'static AnyClass> {
    ensure_frameworks();
    AnyClass::get(name)
}

pub(super) struct NSString(Retained<AnyObject>);

impl NSString {
    pub fn new(s: &str) -> Self {
        unsafe {
            let alloc: Allocated<AnyObject> = msg_send![class!(NSString), alloc];
            Self(msg_send![
                alloc,
                initWithBytes: s.as_ptr() as *const c_void,
                length: s.len(),
                encoding: NS_UTF8_STRING_ENCODING,
            ])
        }
    }

    pub fn as_obj(&self) -> &AnyObject {
        &self.0
    }
}

pub(super) fn has_bundle_id() -> bool {
    ensure_frameworks();
    unsafe {
        let bundle: Retained<AnyObject> = msg_send![class!(NSBundle), mainBundle];
        let id: Option<Retained<AnyObject>> = msg_send![&*bundle, bundleIdentifier];
        id.is_some()
    }
}
