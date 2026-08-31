use crate::notify::data_types::SendNotificationResult;
use std::collections::HashMap;
use zbus::blocking::Connection;
use zbus::zvariant::Value;

#[derive(Clone, Debug)]
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
    pub hints: HashMap<String, Value<'static>>,
    pub timeout: i32,
}

impl NotificationMeta {
    pub fn into_zbus_meta(
        &self,
    ) -> (
        String,
        u32,
        String,
        String,
        String,
        Vec<String>,
        HashMap<String, Value<'static>>,
        i32,
    ) {
        (
            self.app_name.clone(),
            self.replaces_id.clone(),
            self.app_icon.clone(),
            self.summary.clone(),
            self.body.clone(),
            self.actions.clone(),
            self.hints.clone(),
            self.timeout,
        )
    }
}

pub fn send_notification(title: String, description: String) -> i32 {
    let connection = match Connection::session() {
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

    let result = connection.call_method(
        Some("org.freedesktop.Notifications"),
        "/org/freedesktop/Notifications",
        Some("org.freedesktop.Notifications"),
        "Notify",
        &meta.into_zbus_meta(),
    );

    match result {
        Ok(_) => SendNotificationResult::Sent as i32,
        Err(_) => SendNotificationResult::CallFailed as i32,
    }
}
