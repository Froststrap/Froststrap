// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

mod center;
mod runtime;

use std::time::Duration;

use crate::data_types::{NotificationPermissionResult as Perm, SendNotificationResult as Send};
use center::{NotificationCenter, PostError};

pub fn request_notification_permission() -> i32 {
    if !runtime::has_bundle_id() {
        return Perm::NoBundleContext as i32;
    }
    let Some(center) = NotificationCenter::current() else {
        return Perm::NoBundleContext as i32;
    };
    match center.request_authorization(Duration::from_secs(30)) {
        Some(true) => Perm::Granted as i32,
        Some(false) => Perm::Denied as i32,
        None => Perm::TimedOut as i32,
    }
}

#[unsafe(no_mangle)]
pub fn send_notification(title: String, body: String) -> i32 {
    if !runtime::has_bundle_id() {
        return Send::NoBundleContext as i32;
    }
    let Some(center) = NotificationCenter::current() else {
        return Send::NoBundleContext as i32;
    };
    match center.authorization_status(Duration::from_secs(5)) {
        Some(s) if s.can_deliver() => {}
        Some(_) => return Send::NotAuthorized as i32,
        None => return Send::TimedOut as i32,
    }
    match center.post(&title, &body, Duration::from_secs(5)) {
        Ok(()) => Send::Sent as i32,
        Err(PostError::Os | PostError::Unavailable) => Send::OsError as i32,
        Err(PostError::TimedOut) => Send::TimedOut as i32,
    }
}
