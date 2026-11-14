// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use libloading::os::unix::{Library, RTLD_LAZY};
use objc2::runtime::AnyClass;
use std::{ffi::CStr, sync::OnceLock};

static FRAMEWORKS_LOADED: OnceLock<()> = OnceLock::new();

pub(super) fn class(name: &CStr) -> Option<&'static AnyClass> {
    FRAMEWORKS_LOADED.get_or_init(|| unsafe {
        match Library::open(
            Some("/System/Library/Frameworks/UserNotifications.framework/UserNotifications"),
            RTLD_LAZY,
        ) {
            Ok(l) => _ = l.into_raw(),
            Err(e) => log::error!("Could not load `UserNotifications.framework`: {e}"),
        }
    });

    AnyClass::get(name)
}
