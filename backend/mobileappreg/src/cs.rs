// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

//! Safe CoreServices wrapper module
//! Only implements what is needed for lib to function

#![allow(deprecated)]

use objc2_core_foundation::CFString;
use objc2_core_services::LSSetDefaultHandlerForURLScheme;

/// https://developer.apple.com/documentation/coreservices/1447760-lssetdefaulthandlerforurlscheme?language=objc
pub fn set_default_handler_for_url_scheme(
    in_url_scheme: &str,
    in_handler_bundle_id: &str,
) -> Result<(), i32> {
    let scheme = CFString::from_str(in_url_scheme);
    let bundle_id = CFString::from_str(in_handler_bundle_id);
    let status = unsafe { LSSetDefaultHandlerForURLScheme(&scheme, &bundle_id) };
    if status == 0 { Ok(()) } else { Err(status) }
}
