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
