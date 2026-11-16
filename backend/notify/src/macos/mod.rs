// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

mod center;
mod runtime;

use crate::data_types::{NotificationPermissionResult as Perm, SendNotificationResult as Send};
use center::{AuthStatus, NotificationCenter, PostError};
use objc2_foundation::NSBundle;
use std::time::Duration;

pub fn request_permission() -> i32 {
    if NSBundle::mainBundle().bundleIdentifier().is_none() {
        log::error!("No bundle context!");
        return Perm::NoBundleContext as i32;
    }

    let Some(center) = NotificationCenter::current() else {
        log::error!("Unhandled exception has occured!");
        return Perm::OsError as i32;
    };

    match center.request_authorization(Duration::from_secs(30)) {
        Some(true) => Perm::Granted as i32,
        Some(false) => Perm::Denied as i32,
        None => Perm::TimedOut as i32,
    }
}

pub fn send_notification(title: String, body: String, timeout: Duration) -> i32 {
    if NSBundle::mainBundle().bundleIdentifier().is_none() {
        log::error!("No bundle context!");
        return Send::NoBundleContext as i32;
    }

    let Some(center) = NotificationCenter::current() else {
        log::error!("Unhandled exception has occured!");
        return Perm::OsError as i32;
    };

    match center.authorization_status(Duration::from_secs(5)) {
        Some(s) if s.can_deliver() => {}
        Some(AuthStatus::NotDetermined) => {
            log::warn!("Notification status NotDetermined, requesting authorization");
            if center.request_authorization(Duration::from_secs(30)) != Some(true) {
                return Send::NotAuthorized as i32;
            }
        }
        Some(s) => {
            log::warn!("Notifications not deliverable, status={s:?}");
            return Send::NotAuthorized as i32;
        }
        None => return Send::TimedOut as i32,
    }

    match center.post(&title, &body, timeout) {
        Ok(()) => Send::Sent as i32,
        Err(PostError::Os | PostError::Unavailable) => Send::OsError as i32,
        Err(PostError::TimedOut) => Send::TimedOut as i32,
    }
}
