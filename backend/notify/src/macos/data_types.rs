// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use std::ffi::c_void;

use objc2::{
    class, msg_send,
    rc::{Allocated, Retained},
    runtime::AnyObject,
};

const NS_UTF8_STRING_ENCODING: usize = 4;

pub struct NSString(Retained<AnyObject>);

impl NSString {
    pub fn new(s: &str) -> Self {
        unsafe {
            let alloc: Allocated<AnyObject> = msg_send![class!(NSString), alloc];
            let obj: Retained<AnyObject> = msg_send![
                alloc,
                initWithBytes: s.as_ptr() as *const c_void,
                length: s.len(),
                encoding: NS_UTF8_STRING_ENCODING,
            ];
            Self(obj)
        }
    }

    pub fn as_obj(&self) -> &AnyObject {
        &self.0
    }
}

impl From<&str> for NSString {
    fn from(s: &str) -> Self {
        Self::new(s)
    }
}

pub mod opts {
    pub const BADGE: usize = 1 << 0;
    pub const SOUND: usize = 1 << 1;
    pub const ALERT: usize = 1 << 2;
}
