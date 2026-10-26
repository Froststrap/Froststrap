use std::sync::{Mutex, MutexGuard};

use crate::common::{GameRect, InputEvent, Shared, SharedRef};
use crate::wayland;
use crate::x11;

enum Backend {
    Wayland(wayland::Overlay),
    X11(x11::Backend),
}

impl Backend {
    fn poll_input(&self, out: &mut [InputEvent]) -> usize {
        match self {
            Backend::Wayland(w) => w.poll_input(out),
            Backend::X11(x) => x.poll_input(out),
        }
    }

    fn register_hotkey(&self, id: i32, keysym: u32, evdev_keycode: u32, modifiers: u32) -> i32 {
        match self {
            Backend::X11(x) => x.register_hotkey(id, keysym, modifiers),
            Backend::Wayland(w) => {
                w.register_hotkey(id, keysym, evdev_keycode, modifiers);
                0
            }
        }
    }

    fn unregister_hotkey(&self, id: i32) {
        match self {
            Backend::X11(x) => x.unregister_hotkey(id),
            Backend::Wayland(w) => w.unregister_hotkey(id),
        }
    }

    fn set_key_monitoring(&self, enabled: bool) -> i32 {
        match self {
            Backend::X11(x) => x.set_key_monitoring(enabled),
            Backend::Wayland(w) => { w.set_key_monitoring(enabled); 0 }
        }
    }

    fn is_foreground(&self, pid: i32) -> i32 {
        match self {
            Backend::X11(x) => x.is_foreground(pid),
            Backend::Wayland(w) => { if w.is_foreground(pid) { 1 } else { 0 } }
        }
    }

    fn focus_known(&self) -> bool {
        match self {
            Backend::X11(x) => x.focus_known() != 0,
            Backend::Wayland(w) => w.focus_known(),
        }
    }

    fn is_minimised(&self) -> i32 {
        match self {
            Backend::X11(x) => x.is_minimised(),
            Backend::Wayland(w) => { if w.minimised() { 1 } else { 0 } }
        }
    }

    fn focus_game(&self, pid: i32) -> i32 {
        match self {
            Backend::X11(x) => x.focus_game(pid),
            Backend::Wayland(w) => { w.focus_game(); 0 }
        }
    }

    fn set_input_region(&self, surface: usize, rects: &[[i32; 4]]) -> i32 {
        match self {
            Backend::X11(x) => x.set_input_region(surface as u64, rects),
            Backend::Wayland(w) => { w.set_regions(surface, rects.to_vec()); 0 }
        }
    }

    fn clear_input_region(&self, surface: usize) -> i32 {
        match self {
            Backend::X11(x) => x.clear_input_region(surface as u64),
            Backend::Wayland(w) => { w.clear_regions(surface); 0 }
        }
    }

    fn size(&self, surface: usize) -> (i32, i32) {
        match self {
            Backend::Wayland(w) => w.size(surface),
            Backend::X11(_) => (0, 0),
        }
    }

    fn scale(&self, surface: usize) -> i32 {
        match self {
            Backend::Wayland(w) => w.scale(surface),
            Backend::X11(_) => 1,
        }
    }

    fn submit(&self, surface: usize, pixels: &[u8], width: i32, height: i32, stride: i32) {
        if let Backend::Wayland(w) = self {
            w.submit(surface, pixels, width, height, stride);
        }
    }

    fn set_visible(&self, surface: usize, visible: bool) {
        if let Backend::Wayland(w) = self {
            w.set_visible(surface, visible);
        }
    }

    fn set_surface_rect(&self, surface: usize, x: i32, y: i32, width: i32, height: i32) -> i32 {
        match self {
            Backend::Wayland(w) => { w.set_surface_rect(surface, x, y, width, height); 0 }
            Backend::X11(_) => -1,
        }
    }

    fn uses_native_window(&self) -> bool { matches!(self, Backend::X11(_)) }

    fn game_rect(&self) -> GameRect {
        match self {
            Backend::X11(x) => x.game_rect(),
            Backend::Wayland(w) => w.game_rect(),
        }
    }

    fn shutdown(self) {
        match self {
            Backend::Wayland(w) => w.shutdown(),
            Backend::X11(x) => x.shutdown(),
        }
    }
}

static OVERLAY: Mutex<Option<Backend>> = Mutex::new(None);

fn slot() -> MutexGuard<'static, Option<Backend>> {
    OVERLAY.lock().unwrap_or_else(|p| p.into_inner())
}

fn wayland_session() -> bool {
    std::env::var_os("WAYLAND_DISPLAY").is_some()
        || std::env::var("XDG_SESSION_TYPE").map(|v| v.eq_ignore_ascii_case("wayland")).unwrap_or(false)
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_init(process_id: i32) -> i32 {
    let mut s = slot();
    if s.is_some() {
        return 1;
    }

    let shared = SharedRef::new(Shared::new());

    if wayland_session() {
        match wayland::Overlay::start(process_id) {
            Ok(overlay) => {
                *s = Some(Backend::Wayland(overlay));
                return 0;
            }
            Err(err) => {
                eprintln!("overlay_init: wayland unavailable ({err}), trying X11/XWayland");
            }
        }
    }

    if std::env::var_os("DISPLAY").is_some() {
        match x11::Backend::start(process_id, shared) {
            Ok(x) => { *s = Some(Backend::X11(x)); return 0; }
            Err(err) => eprintln!("overlay_init: x11 failed: {err}"),
        }
    }

    -4
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_shutdown() {
    if let Some(backend) = slot().take() {
        backend.shutdown();
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_uses_native_window() -> i32 {
    slot().as_ref().map_or(0, |b| if b.uses_native_window() { 1 } else { 0 })
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn overlay_poll_input(out: *mut InputEvent, max: i32) -> i32 {
    if out.is_null() || max <= 0 { return 0; }
    let buffer = unsafe { std::slice::from_raw_parts_mut(out, max as usize) };
    slot().as_ref().map_or(0, |b| b.poll_input(buffer) as i32)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn overlay_get_game_rect(out: *mut GameRect) -> i32 {
    if out.is_null() { return -1; }
    match slot().as_ref() {
        Some(b) => { unsafe { *out = b.game_rect() }; 0 }
        None => -1,
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_register_hotkey(id: i32, keysym: u32, evdev_keycode: u32, modifiers: u32) -> i32 {
    match slot().as_ref() {
        Some(b) => b.register_hotkey(id, keysym, evdev_keycode, modifiers),
        None => -1,
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_unregister_hotkey(id: i32) {
    if let Some(b) = slot().as_ref() { b.unregister_hotkey(id); }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_set_key_monitoring(enabled: i32) -> i32 {
    slot().as_ref().map_or(-1, |b| b.set_key_monitoring(enabled != 0))
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_is_foreground(pid: i32) -> i32 {
    slot().as_ref().map_or(0, |b| b.is_foreground(pid))
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_is_minimised() -> i32 {
    slot().as_ref().map_or(0, |b| b.is_minimised())
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_focus_known() -> i32 {
    slot().as_ref().map_or(1, |b| if b.focus_known() { 1 } else { 0 })
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_scale(surface: i32) -> i32 {
    slot().as_ref().map_or(1, |b| b.scale(surface.max(0) as usize))
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_focus_game(pid: i32) -> i32 {
    slot().as_ref().map_or(-1, |b| b.focus_game(pid))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn overlay_set_input_region(surface: i32, rects: *const i32, count: i32) -> i32 {
    if surface < 0 || count < 0 || (count > 0 && rects.is_null()) { return -1; }
    let flat: &[i32] = if count == 0 { &[] } else { unsafe { std::slice::from_raw_parts(rects, count as usize * 4) } };
    let list: Vec<[i32; 4]> = flat.chunks_exact(4).map(|c| [c[0], c[1], c[2], c[3]]).collect();
    slot().as_ref().map_or(-1, |b| b.set_input_region(surface as usize, &list))
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_clear_input_region(surface: i32) -> i32 {
    if surface < 0 { return -1; }
    slot().as_ref().map_or(-1, |b| b.clear_input_region(surface as usize))
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_width(surface: i32) -> i32 {
    slot().as_ref().map_or(0, |b| b.size(surface.max(0) as usize).0)
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_height(surface: i32) -> i32 {
    slot().as_ref().map_or(0, |b| b.size(surface.max(0) as usize).1)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn overlay_submit(surface: i32, pixels: *const u8, width: i32, height: i32, stride: i32) -> i32 {
    if surface < 0 || pixels.is_null() || width <= 0 || height <= 0 || stride < width.saturating_mul(4) {
        return -1;
    }
    let len = stride as usize * (height as usize - 1) + width as usize * 4;
    let data = unsafe { std::slice::from_raw_parts(pixels, len) };
    match slot().as_ref() {
        Some(b) => { b.submit(surface as usize, data, width, height, stride); 0 }
        None => -1,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn overlay_set_regions(surface: i32, rects: *const i32, count: i32) -> i32 {
    unsafe { overlay_set_input_region(surface, rects, count) }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_set_visible(surface: i32, visible: i32) {
    if let Some(b) = slot().as_ref() {
        b.set_visible(surface.max(0) as usize, visible != 0);
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_set_surface_rect(surface: i32, x: i32, y: i32, width: i32, height: i32) -> i32 {
    if surface < 0 { return -1; }
    slot().as_ref().map_or(-1, |b| b.set_surface_rect(surface as usize, x, y, width, height))
}