// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use super::runtime::{NSString, class};
use super::slot::{fulfill, new_slot, wait_for};
use block2::RcBlock;
use objc2::rc::Retained;
use objc2::runtime::{AnyObject, Bool};
use objc2::{class, msg_send};
use std::sync::Arc;

const OPT_BADGE: usize = 1 << 0;
const OPT_SOUND: usize = 1 << 1;
const OPT_ALERT: usize = 1 << 2;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(super) enum AuthStatus {
    NotDetermined,
    Denied,
    Authorized,
    Provisional,
    Ephemeral,
    Unknown(isize),
}

impl From<isize> for AuthStatus {
    fn from(v: isize) -> Self {
        match v {
            0 => Self::NotDetermined,
            1 => Self::Denied,
            2 => Self::Authorized,
            3 => Self::Provisional,
            4 => Self::Ephemeral,
            n => Self::Unknown(n),
        }
    }
}

impl AuthStatus {
    pub fn can_deliver(self) -> bool {
        matches!(self, Self::Authorized | Self::Provisional)
    }
}

#[derive(Debug)]
pub(super) enum PostError {
    Unavailable,
    Os,
    TimedOut,
}

pub(super) struct NotificationCenter(Retained<AnyObject>);

impl NotificationCenter {
    pub fn current() -> Option<Self> {
        let cls = class(c"UNUserNotificationCenter")?;
        let obj: Option<Retained<AnyObject>> = unsafe { msg_send![cls, currentNotificationCenter] };
        obj.map(Self)
    }

    pub fn request_authorization(&self, timeout_secs: u64) -> Option<bool> {
        let slot = new_slot::<bool>();
        let tx = Arc::clone(&slot);
        let handler = RcBlock::new(move |granted: Bool, _err: *mut AnyObject| {
            fulfill(&tx, granted.as_bool());
        });
        unsafe {
            let _: () = msg_send![
                &*self.0,
                requestAuthorizationWithOptions: OPT_ALERT | OPT_SOUND | OPT_BADGE,
                completionHandler: &*handler,
            ];
        }
        wait_for(&slot, timeout_secs)
    }

    pub fn authorization_status(&self, timeout_secs: u64) -> Option<AuthStatus> {
        let slot = new_slot::<isize>();
        let tx = Arc::clone(&slot);
        let handler = RcBlock::new(move |settings: *mut AnyObject| {
            if settings.is_null() {
                return;
            }
            let raw: isize = unsafe { msg_send![&*settings, authorizationStatus] };
            fulfill(&tx, raw);
        });
        unsafe {
            let _: () =
                msg_send![&*self.0, getNotificationSettingsWithCompletionHandler: &*handler];
        }
        wait_for(&slot, timeout_secs).map(AuthStatus::from)
    }

    pub fn post(&self, title: &str, body: &str, timeout_secs: u64) -> Result<(), PostError> {
        let (Some(content_cls), Some(request_cls)) = (
            class(c"UNMutableNotificationContent"),
            class(c"UNNotificationRequest"),
        ) else {
            return Err(PostError::Unavailable);
        };

        let request: Retained<AnyObject> = unsafe {
            let content: Retained<AnyObject> = msg_send![content_cls, new];
            let _: () = msg_send![&*content, setTitle: NSString::new(title).as_obj()];
            let _: () = msg_send![&*content, setBody: NSString::new(body).as_obj()];

            let uuid: Retained<AnyObject> = msg_send![class!(NSUUID), UUID];
            let id: Retained<AnyObject> = msg_send![&*uuid, UUIDString];

            msg_send![
                request_cls,
                requestWithIdentifier: &*id,
                content: &*content,
                trigger: None::<&AnyObject>,
            ]
        };

        let slot = new_slot::<bool>();
        let tx = Arc::clone(&slot);
        let completion = RcBlock::new(move |err: *mut AnyObject| fulfill(&tx, err.is_null()));
        unsafe {
            let _: () = msg_send![
                &*self.0,
                addNotificationRequest: &*request,
                withCompletionHandler: &*completion,
            ];
        }

        match wait_for(&slot, timeout_secs) {
            Some(true) => Ok(()),
            Some(false) => Err(PostError::Os),
            None => Err(PostError::TimedOut),
        }
    }
}
