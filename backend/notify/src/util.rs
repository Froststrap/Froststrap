//! Utilities for the FFI boundary.

use std::ffi::CStr;
use std::os::raw::c_char;

/// Implements tools to get from FFI string.
pub struct IStr(pub String);

impl From<String> for IStr {
    fn from(input: String) -> Self {
         Self(input)   
    }
}

impl From<IStr> for String {
    fn from(input: IStr) -> Self {
        input.0
    }
}

impl From<*const c_char> for IStr {
    fn from(input: *const c_char) -> Self {
        if input.is_null() {
            return Self("".into());
        }

        // SAFETY: We know that input it not null
        //
        // Though it is up to caller with the c_str
        // to provide a good string otherwise it would
        // break the program anyways.
        let cstr = unsafe {
            CStr::from_ptr(input)
        };

        cstr.to_string_lossy().into_owned().into()
    }
}
