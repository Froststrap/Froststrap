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

impl Into<String> for IStr {
    fn into(self) -> String {
        self.0
    }
}

impl From<*const c_char> for IStr {
    fn from(input: *const c_char) -> Self {
        if input.is_null() {
            return Self("".into());
        }

        // SAFETY: We know that input it not null
        // & this checks for null termination.
        //
        // Though it is up to caller with the c_str
        // to provide its not a retarded string which
        // it not even a string, which would break
        // the program anyways.
        let cstr = unsafe {
            CStr::from_ptr(input)
        };

        cstr.to_string_lossy().into_owned().into()
    }
}
