mod foreign_toplevel;
mod fullscreen;
mod gnome;
mod hyprland;
mod kde;

use std::sync::mpsc::Receiver;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SoberRect {
    pub monitor: i64,
    pub x: i32,
    pub y: i32,
    pub width: i32,
    pub height: i32,
}

pub fn output_hash(name: &str) -> i64 {
    let mut hash: u64 = 0xcbf2_9ce4_8422_2325;
    for byte in name.as_bytes() {
        hash ^= u64::from(*byte);
        hash = hash.wrapping_mul(0x0000_0100_0000_01b3);
    }
    hash as i64
}

pub trait WindowTracker: Send {
    fn query(&self) -> Option<SoberRect>;
    fn watch(&self) -> Option<Receiver<()>>;
    fn uses_output(&self) -> bool { false }
    fn focus(&self) {}
    fn foreground(&self) -> i32 { 0 }
    fn minimised(&self) -> bool { false }
    fn focus_known(&self) -> bool { true }
    fn game_output(&self) -> Option<i64> { None }
}

pub fn detect(process_id: i32) -> Option<Box<dyn WindowTracker>> {
    if std::env::var_os("HYPRLAND_INSTANCE_SIGNATURE").is_some() {
        return Some(Box::new(hyprland::Hyprland::new(process_id)));
    }

    if let Some(tracker) = kde::Kde::try_new(process_id) {
        return Some(Box::new(tracker));
    }

    if let Some(tracker) = foreign_toplevel::ForeignToplevel::try_new(process_id) {
        return Some(Box::new(tracker));
    }

    if let Some(tracker) = gnome::Gnome::try_new(process_id) {
        return Some(Box::new(tracker));
    }

    if std::env::var_os("WAYLAND_DISPLAY").is_some() {
        return Some(Box::new(fullscreen::Fullscreen));
    }

    None
}