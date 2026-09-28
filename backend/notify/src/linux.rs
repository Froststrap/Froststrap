// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use crate::data_types::{NotificationMeta, ReplacesId, SendNotificationResult};
use rustbus::{MessageBuilder, RpcConn, connection::Timeout, message_builder::MessageType};
use std::{collections::HashMap, time::Duration};

pub fn send_notification(title: String, description: String) -> i32 {
    let timeout = Timeout::Duration(Duration::from_secs(2));

    let mut conn = match RpcConn::session_conn(timeout) {
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

    let mut msg = MessageBuilder::new()
        .call("Notify")
        .with_interface("org.freedesktop.Notifications")
        .on("/org/freedesktop/Notifications")
        .at("org.freedesktop.Notifications")
        .build();

    if meta.write_body(&mut msg).is_err() {
        return SendNotificationResult::CallFailed as i32;
    }

    let id = match conn
        .send_message(&mut msg)
        .and_then(|ctx| ctx.write_all().map_err(|e| e.1))
    {
        Ok(id) => id,
        Err(_) => return SendNotificationResult::CallFailed as i32,
    };

    match conn.wait_response(id, timeout) {
        Ok(reply) if reply.typ == MessageType::Error => SendNotificationResult::CallFailed as i32,
        Ok(_) => SendNotificationResult::Sent as i32,
        Err(_) => SendNotificationResult::CallFailed as i32,
    }
}
