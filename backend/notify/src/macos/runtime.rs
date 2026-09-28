// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use objc2::rc::Retained;
use objc2::runtime::{AnyClass, AnyObject};
use objc2::{class, msg_send};
use std::ffi::{CStr, c_char, c_int, c_void};
use std::sync::OnceLock;

fn ensure_frameworks() {
    const RTLD_LAZY: c_int = 0x1;

    unsafe extern "C" {
        fn dlopen(path: *const c_char, mode: c_int) -> *mut c_void;
    }

    static FRAMEWORKS_LOADED: OnceLock<()> = OnceLock::new();
    FRAMEWORKS_LOADED.get_or_init(|| unsafe {
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

pub(super) fn has_bundle_id() -> bool {
    ensure_frameworks();
    unsafe {
        let bundle: Retained<AnyObject> = msg_send![class!(NSBundle), mainBundle];
        let id: Option<Retained<AnyObject>> = msg_send![&*bundle, bundleIdentifier];
        id.is_some()
    }
}
