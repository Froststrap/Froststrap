use block2::RcBlock;
use objc2::runtime::Bool;
use objc2_foundation::{MainThreadMarker, NSBundle, NSError, NSString, NSUUID};
use objc2_user_notifications::{
    UNAuthorizationOptions, UNAuthorizationStatus, UNMutableNotificationContent,
    UNNotificationRequest, UNUserNotificationCenter,
};
use std::sync::{Arc, Condvar, Mutex};
use std::time::Duration;

fn has_valid_bundle_context() -> bool {
    NSBundle::mainBundle().bundleIdentifier().is_some()
}

#[repr(i32)]
pub enum NotificationPermissionResult {
    Granted,
    Denied,
    TimedOut,
    NoBundleContext,
}

#[repr(i32)]
pub enum SendNotificationResult {
    Sent = 0,
    NotAuthorized = 1,
    InvalidUtf8 = 2,
    NoBundleContext = 3,
    TimedOut = 4,
    OsError = 5,
}

pub fn request_notification_permission() -> i32 {
    if !has_valid_bundle_context() {
        return NotificationPermissionResult::NoBundleContext as i32;
    }

    let _ = MainThreadMarker::new();

    let center = UNUserNotificationCenter::currentNotificationCenter();
    let options = UNAuthorizationOptions::Alert
        | UNAuthorizationOptions::Sound
        | UNAuthorizationOptions::Badge;

    let pair = Arc::new((Mutex::new(None::<bool>), Condvar::new()));
    let pair_for_block = Arc::clone(&pair);

    let completion_handler = RcBlock::new(move |granted: Bool, _error: *mut NSError| {
        let (lock, cvar) = &*pair_for_block;
        *lock.lock().unwrap() = Some(granted.as_bool());
        cvar.notify_one();
    });

    center.requestAuthorizationWithOptions_completionHandler(options, &completion_handler);

    let (lock, cvar) = &*pair;
    let guard = lock.lock().unwrap();
    let (guard, timeout) = cvar
        .wait_timeout_while(guard, Duration::from_secs(30), |r| r.is_none())
        .unwrap();

    match *guard {
        Some(true) => NotificationPermissionResult::Granted as i32,
        Some(false) => NotificationPermissionResult::Denied as i32,
        None => {
            debug_assert!(timeout.timed_out());
            NotificationPermissionResult::TimedOut as i32
        }
    }
}

fn current_authorization_status(
    center: &UNUserNotificationCenter,
) -> Option<UNAuthorizationStatus> {
    let pair = Arc::new((Mutex::new(None::<UNAuthorizationStatus>), Condvar::new()));
    let pair_for_block = Arc::clone(&pair);

    let handler = RcBlock::new(
        move |settings: std::ptr::NonNull<objc2_user_notifications::UNNotificationSettings>| {
            let status = unsafe { settings.as_ref().authorizationStatus() };
            let (lock, cvar) = &*pair_for_block;
            *lock.lock().unwrap() = Some(status);
            cvar.notify_one();
        },
    );

    center.getNotificationSettingsWithCompletionHandler(&handler);

    let (lock, cvar) = &*pair;
    let guard = lock.lock().unwrap();
    let (guard, timeout) = cvar
        .wait_timeout_while(guard, Duration::from_secs(5), |r| r.is_none())
        .unwrap();
    if timeout.timed_out() { None } else { *guard }
}

#[unsafe(no_mangle)]
pub fn send_notification(title: String, body: String) -> i32 {
    if !has_valid_bundle_context() {
        return SendNotificationResult::NoBundleContext as i32;
    }

    let center = UNUserNotificationCenter::currentNotificationCenter();

    match current_authorization_status(&center) {
        Some(UNAuthorizationStatus::Authorized) | Some(UNAuthorizationStatus::Provisional) => {}
        Some(_) => return SendNotificationResult::NotAuthorized as i32,
        None => return SendNotificationResult::TimedOut as i32,
    }

    let content = {
        let c = UNMutableNotificationContent::new();
        c.setTitle(&NSString::from_str(&title));
        c.setBody(&NSString::from_str(&body));
        c
    };

    let identifier = NSUUID::new().UUIDString();
    let request =
        UNNotificationRequest::requestWithIdentifier_content_trigger(&identifier, &content, None);

    let pair = Arc::new((Mutex::new(None::<Result<(), String>>), Condvar::new()));
    let pair_for_block = Arc::clone(&pair);

    let completion = RcBlock::new(move |error: *mut NSError| {
        let result = if error.is_null() {
            Ok(())
        } else {
            let desc = unsafe { (*error).localizedDescription().to_string() };
            Err(desc)
        };
        let (lock, cvar) = &*pair_for_block;
        *lock.lock().unwrap() = Some(result);
        cvar.notify_one();
    });

    center.addNotificationRequest_withCompletionHandler(&request, Some(&completion));

    let (lock, cvar) = &*pair;
    let guard = lock.lock().unwrap();
    let (guard, timeout) = cvar
        .wait_timeout_while(guard, Duration::from_secs(5), |r| r.is_none())
        .unwrap();

    match &*guard {
        Some(Ok(())) => SendNotificationResult::Sent as i32,
        Some(Err(_)) => SendNotificationResult::OsError as i32,
        None => {
            debug_assert!(timeout.timed_out());
            SendNotificationResult::TimedOut as i32
        }
    }
}
