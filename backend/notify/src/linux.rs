// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use crate::data_types::SendNotificationResult;
use rustbus::{
    MessageBuilder, RpcConn, connection::Timeout, message_builder::MarshalledMessage,
    message_builder::MessageType, wire::errors::MarshalError, wire::marshal::traits::Variant,
};
use std::{collections::HashMap, time::Duration};

struct NotificationMeta {
    pub app_name: String,
    /// 0 = new notification
    pub replaces_id: u32,
    pub app_icon: String,
    /// Title
    pub summary: String,
    /// Body of notif
    pub body: String,
    pub actions: Vec<String>,
    pub hints: HashMap<String, Variant<String>>,
    pub timeout: i32,
}

impl NotificationMeta {
    fn write_body(&self, msg: &mut MarshalledMessage) -> Result<(), MarshalError> {
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

pub fn send_notification(title: String, description: String) -> i32 {
    let timeout = Timeout::Duration(Duration::from_secs(2));

    let mut conn = match RpcConn::session_conn(timeout) {
        Ok(c) => c,
        Err(_) => return SendNotificationResult::ConnectionFailed as i32,
    };

    let meta = NotificationMeta {
        app_name: "Froststrap".into(),
        replaces_id: 0,
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
