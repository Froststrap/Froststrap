// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use std::{collections::HashMap, num::NonZeroU32};

#[cfg(target_os = "linux")]
use rustbus::{
    Marshal, Signature,
    message_builder::MarshalledMessage,
    wire::{errors::MarshalError, marshal::traits::Variant},
};

#[repr(i32)]
pub enum NotificationPermissionResult {
    Granted,
    Denied,
    TimedOut,
    NoBundleContext,
}

#[repr(i32)]
pub enum SendNotificationResult {
    Sent,
    NotAuthorized,
    InvalidUtf8,
    NoBundleContext,
    TimedOut,
    OsError,
    CallFailed,
    ConnectionFailed,
}

#[repr(i32)]
pub enum SetApplicationResult {
    Set,
    InvalidUtf8,
}

pub struct NotificationMeta {
    pub app_name: String,
    /// 0 = new notification
    pub replaces_id: ReplacesId,
    pub app_icon: String,
    /// Title
    pub summary: String,
    /// Body of notif
    pub body: String,
    pub actions: Vec<String>,
    pub hints: HashMap<String, Variant<String>>,
    pub timeout: i32,
}

#[derive(Clone, Copy)]
pub enum ReplacesId {
    New,
    Val(NonZeroU32),
}

impl Signature for ReplacesId {
    fn signature() -> rustbus::signature::Type {
        u32::signature()
    }

    fn alignment() -> usize {
        u32::alignment()
    }
}

impl From<u32> for ReplacesId {
    fn from(f: u32) -> Self {
        match f {
            0 => Self::New,
            x => Self::Val(x.try_into().unwrap()),
        }
    }
}

impl Marshal for ReplacesId {
    fn marshal(
        &self,
        ctx: &mut rustbus::wire::marshal::MarshalContext,
    ) -> Result<(), rustbus::wire::errors::MarshalError> {
        match *self {
            Self::New => 0,
            Self::Val(x) => x.into(),
        }
        .marshal(ctx)
    }
}

impl NotificationMeta {
    pub fn write_body(&self, msg: &mut MarshalledMessage) -> Result<(), MarshalError> {
        let actions: Vec<&str> = self.actions.iter().map(String::as_str).collect();
        let hints: HashMap<&str, &Variant<String>> =
            self.hints.iter().map(|(k, v)| (k.as_str(), v)).collect();

        msg.body.push_param(self.app_name.as_str())?;
        msg.body.push_param(self.replaces_id)?;
        msg.body.push_param(self.app_icon.as_str())?;
        msg.body.push_param(self.summary.as_str())?;
        msg.body.push_param(self.body.as_str())?;
        msg.body.push_param(&actions[..])?;
        msg.body.push_param(&hints)?;
        msg.body.push_param(self.timeout)?;
        Ok(())
    }
}
