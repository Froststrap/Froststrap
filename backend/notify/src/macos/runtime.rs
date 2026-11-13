// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use objc2::runtime::AnyClass;
use objc2_foundation::NSBundle;
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
    NSBundle::mainBundle().bundleIdentifier().is_some()
}
