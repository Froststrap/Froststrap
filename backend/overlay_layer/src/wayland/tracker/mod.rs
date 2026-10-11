mod hyprland;

use std::sync::mpsc::Receiver;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SoberRect {
    pub monitor: i64,
    pub x: i32,
    pub y: i32,
    pub width: i32,
    pub height: i32,
}

pub trait WindowTracker: Send {
    fn query(&self) -> Option<SoberRect>;
    fn watch(&self) -> Option<Receiver<()>>;
}

pub fn detect() -> Option<Box<dyn WindowTracker>> {
    if std::env::var_os("HYPRLAND_INSTANCE_SIGNATURE").is_some() {
        return Some(Box::new(hyprland::Hyprland));
    }
    None
}
