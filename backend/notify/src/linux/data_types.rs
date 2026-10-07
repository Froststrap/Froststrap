use {
    dbus::arg::Variant,
    std::{collections::HashMap, num::NonZeroU32},
};

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
#[cfg(target_os = "linux")]
pub enum ReplacesId {
    New,
    Val(NonZeroU32),
}

#[cfg(target_os = "linux")]
impl ReplacesId {
    pub fn as_u32(self) -> u32 {
        match self {
            Self::New => 0,
            Self::Val(x) => x.get(),
        }
    }
}

#[cfg(target_os = "linux")]
impl From<u32> for ReplacesId {
    fn from(f: u32) -> Self {
        NonZeroU32::new(f).map_or(Self::New, Self::Val)
    }
}

#[cfg(target_os = "linux")]
impl NotificationMeta {
    /// Args in the order/signature of Notify: (susssasa{sv}i)
    pub fn into_args(
        self,
    ) -> (
        String,
        u32,
        String,
        String,
        String,
        Vec<String>,
        HashMap<String, Variant<String>>,
        i32,
    ) {
        (
            self.app_name,
            self.replaces_id.as_u32(),
            self.app_icon,
            self.summary,
            self.body,
            self.actions,
            self.hints,
            self.timeout,
        )
    }
}
