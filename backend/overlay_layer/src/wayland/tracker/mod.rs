// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

mod hyprland;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SoberRect {
    pub monitor: i64,
    pub x: i32,
    pub y: i32,
    pub width: i32,
    pub height: i32,
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct GameInfo {
    pub rect: Option<SoberRect>,
    pub present: bool,
    pub visible: bool,
    pub foreground: bool,
    pub fullscreen: bool,
}

pub trait WindowTracker: Send {
    fn game_info(&self) -> GameInfo;
    fn focus_game(&self) -> bool;
}

pub fn detect() -> Option<Box<dyn WindowTracker>> {
    if std::env::var_os("HYPRLAND_INSTANCE_SIGNATURE").is_some() {
        return Some(Box::new(hyprland::Hyprland));
    }
    None
}
