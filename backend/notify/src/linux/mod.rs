// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

mod data_types;

use crate::data_types::SendNotificationResult;
use data_types::{NotificationMeta, ReplacesId};
use dbus::blocking::Connection;
use std::{collections::HashMap, time::Duration};

pub fn send_notification(title: String, description: String) -> i32 {
    let timeout = Duration::from_secs(2);

    let conn = match Connection::new_session() {
        Ok(c) => c,
        Err(_) => return SendNotificationResult::ConnectionFailed as i32,
    };

    let meta = NotificationMeta {
        app_name: "Froststrap".into(),
        replaces_id: ReplacesId::New,
        app_icon: "dialog-information".into(),
        summary: title,
        body: description,
        actions: Vec::new(),
        hints: HashMap::new(),
        timeout: 3000,
    };

    let proxy = conn.with_proxy(
        "org.freedesktop.Notifications",
        "/org/freedesktop/Notifications",
        timeout,
    );

    let result: Result<(u32,), dbus::Error> = proxy.method_call(
        "org.freedesktop.Notifications",
        "Notify",
        meta.into_args(),
    );

    match result {
        Ok(_id) => SendNotificationResult::Sent as i32,
        Err(e) if e.name() == Some("org.freedesktop.DBus.Error.NoReply") => {
            SendNotificationResult::TimedOut as i32
        }
        Err(_) => SendNotificationResult::CallFailed as i32,
    }
}
