use std::sync::RwLock;

use crate::notify::data_types::SendNotificationResult;
use windows::Data::Xml::Dom::XmlDocument;
use windows::UI::Notifications::{ToastNotification, ToastNotificationManager, ToastTemplateType};
use windows::core::HSTRING;

static AUMID_LOCK: RwLock<Option<String>> = RwLock::new(None);

pub fn set_application(aumid: String) -> i32 {
    let mut writer = AUMID_LOCK.write().unwrap();

    *writer = Some(aumid);

    0 // TODO: make this actually call a threaded function which Windows will
    //// understand context of it in the thread rather than passing it every time.
}

pub fn send_notification(title: String, body: String) -> i32 {
    match send_notification_impl(&title, &body) {
        Ok(()) => SendNotificationResult::Sent as i32,
        Err(_e) => SendNotificationResult::CallFailed as i32,
    }
}

fn send_notification_impl(title: &str, body: &str) -> windows::core::Result<()> {
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

    let aumid_reader = AUMID_LOCK
        .read()
        .unwrap();
    let aumid = aumid_reader.clone().expect("set_application() must be run before to send a notification with an AUMID.");

    let notifier = ToastNotificationManager::CreateToastNotifierWithId(&HSTRING::from(aumid))?;
    notifier.Show(&toast)?;

    Ok(())
}
