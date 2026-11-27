// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use std::sync::RwLock;

use crate::data_types::SendNotificationResult;
use windows::Data::Xml::Dom::XmlDocument;
use windows::UI::Notifications::{ToastNotification, ToastNotificationManager, ToastTemplateType};
use windows::core::HSTRING;
use std::time::Duration;

static AUMID_LOCK: RwLock<Option<String>> = RwLock::new(None);

pub fn init(aumid: String) -> i32 {
    let mut writer = AUMID_LOCK.write().unwrap_or_else(|e| e.into_inner());

    *writer = Some(aumid);

    // TODO: make this actually call a threaded function which Windows will
    //       understand context of it in the thread rather than passing it every time.

    0
}

fn show_toast(aumid: &str, title: String, body: String) -> windows::core::Result<()> {
    let toast_xml: XmlDocument =
        ToastNotificationManager::GetTemplateContent(ToastTemplateType::ToastText02)?;

    let text_nodes = toast_xml.GetElementsByTagName(&HSTRING::from("text"))?;

    let title_node = text_nodes.Item(0)?;
    let title_text = toast_xml.CreateTextNode(&HSTRING::from(title))?;
    title_node.AppendChild(&title_text)?;

    let body_node = text_nodes.Item(1)?;
    let body_text = toast_xml.CreateTextNode(&HSTRING::from(body))?;
    body_node.AppendChild(&body_text)?;

    let toast = ToastNotification::CreateToastNotification(&toast_xml)?;

    let notifier = ToastNotificationManager::CreateToastNotifierWithId(&HSTRING::from(aumid))?;
    notifier.Show(&toast)
}

pub fn send_notification(title: String, body: String, _timeout: Duration) -> i32 {
    let aumid = AUMID_LOCK.read().unwrap_or_else(|e| e.into_inner()).clone();
    let Some(aumid) = aumid else {
        log::error!("init() must be called before sending a notification");
        return SendNotificationResult::NoBundleContext as i32;
    };

    match show_toast(&aumid, title, body) {
        Ok(()) => SendNotificationResult::Sent as i32,
        Err(e) => {
            log::error!("Failed to show toast notification: {e}");
            SendNotificationResult::OsError as i32
        }
    }
}
