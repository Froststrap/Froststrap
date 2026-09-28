// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use super::data_types::{NSString, opts};
use super::runtime::class;
use block2::RcBlock;
use objc2::rc::Retained;
use objc2::runtime::{AnyObject, Bool};
use objc2::{class, msg_send};
use std::sync::mpsc::{RecvTimeoutError, channel};
use std::time::Duration;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
#[repr(isize)]
pub(super) enum AuthStatus {
    NotDetermined = 0,
    Denied = 1,
    Authorized = 2,
    Provisional = 3,
    Ephemeral = 4,
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

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(super) enum PostError {
    Unavailable,
    Os,
    TimedOut,
}

impl std::fmt::Display for PostError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::Unavailable => write!(f, "User notifications framework unavailable"),
            Self::Os => write!(f, "OS failed to deliver notification request"),
            Self::TimedOut => write!(f, "Notification request timed out"),
        }
    }
}

impl std::error::Error for PostError {}

pub(super) struct NotificationCenter(Retained<AnyObject>);

impl NotificationCenter {
    pub fn current() -> Option<Self> {
        let cls = class(c"UNUserNotificationCenter")?;
        let obj: Option<Retained<AnyObject>> = unsafe { msg_send![cls, currentNotificationCenter] };
        obj.map(Self)
    }

    pub fn request_authorization(&self, timeout: Duration) -> Option<bool> {
        let (tx, rx) = channel();
        let handler = RcBlock::new(move |granted: Bool, _err: Option<&AnyObject>| {
            let _ = tx.send(granted.as_bool());
        });

        unsafe {
            let _: () = msg_send![
                &*self.0,
                requestAuthorizationWithOptions: opts::ALERT | opts::SOUND | opts::BADGE,
                completionHandler: &*handler,
            ];
        }

        rx.recv_timeout(timeout).ok()
    }

    pub fn authorization_status(&self, timeout: Duration) -> Option<AuthStatus> {
        let (tx, rx) = channel();
        let handler = RcBlock::new(move |settings: Option<&AnyObject>| {
            if let Some(settings) = settings {
                let raw: isize = unsafe { msg_send![settings, authorizationStatus] };
                let _ = tx.send(raw);
            }
        });

        unsafe {
            let _: () =
                msg_send![&*self.0, getNotificationSettingsWithCompletionHandler: &*handler];
        }

        rx.recv_timeout(timeout).ok().map(AuthStatus::from)
    }

    pub fn post(&self, title: &str, body: &str, timeout: Duration) -> Result<(), PostError> {
        let content_cls = class(c"UNMutableNotificationContent").ok_or(PostError::Unavailable)?;
        let request_cls = class(c"UNNotificationRequest").ok_or(PostError::Unavailable)?;

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

        let (tx, rx) = channel();
        let completion = RcBlock::new(move |err: Option<&AnyObject>| {
            let _ = tx.send(err.is_none());
        });

        unsafe {
            let _: () = msg_send![
                &*self.0,
                addNotificationRequest: &*request,
                withCompletionHandler: &*completion,
            ];
        }

        match rx.recv_timeout(timeout) {
            Ok(true) => Ok(()),
            Ok(false) => Err(PostError::Os),
            Err(RecvTimeoutError::Timeout | RecvTimeoutError::Disconnected) => {
                Err(PostError::TimedOut)
            }
        }
    }
}
