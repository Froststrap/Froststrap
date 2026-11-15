use std::sync::{Mutex, MutexGuard};

use crate::input::{GlobalInput, KeyState};
use crate::wayland::{GameState, InputEvent, Overlay};

struct Backend {
    overlay: Overlay,
    input: GlobalInput,
}

static BACKEND: Mutex<Option<Backend>> = Mutex::new(None);

fn slot() -> MutexGuard<'static, Option<Backend>> {
    BACKEND
        .lock()
        .unwrap_or_else(|poisoned| poisoned.into_inner())
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_init() -> i32 {
    let mut slot = slot();

    if slot.is_some() {
        return 1;
    }

    *slot = Some(Backend {
        overlay: Overlay::start(),
        input: GlobalInput::start(),
    });

    0
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_shutdown() {
    let backend = slot().take();

    if let Some(backend) = backend {
        backend.input.stop();
        backend.overlay.shutdown();
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_create_surface(tracked: i32) -> i32 {
    match slot().as_ref() {
        Some(backend) => backend.overlay.create_surface(tracked != 0),
        None => -1,
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_destroy_surface(id: i32) {
    if let Some(backend) = slot().as_ref() {
        backend.overlay.destroy_surface(id);
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_set_visible(id: i32, visible: i32) {
    if let Some(backend) = slot().as_ref() {
        backend.overlay.set_visible(id, visible != 0);
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_set_rect(id: i32, x: i32, y: i32, width: i32, height: i32) {
    if let Some(backend) = slot().as_ref() {
        backend.overlay.set_rect(id, x, y, width, height);
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_set_keyboard(id: i32, mode: i32) {
    if let Some(backend) = slot().as_ref() {
        backend.overlay.set_keyboard(id, mode);
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn overlay_set_regions(id: i32, rects: *const i32, count: i32) -> i32 {
    if count < 0 || (count > 0 && rects.is_null()) {
        return -1;
    }

    let flat: &[i32] = if count == 0 {
        &[]
    } else {
        unsafe { std::slice::from_raw_parts(rects, count as usize * 4) }
    };

    let list: Vec<[i32; 4]> = flat
        .chunks_exact(4)
        .map(|chunk| [chunk[0], chunk[1], chunk[2], chunk[3]])
        .collect();

    match slot().as_ref() {
        Some(backend) => {
            backend.overlay.set_regions(id, list);
            0
        }
        None => -1,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn overlay_submit(
    id: i32,
    pixels: *const u8,
    width: i32,
    height: i32,
    stride: i32,
) -> i32 {
    if pixels.is_null() || width <= 0 || height <= 0 || stride < width.saturating_mul(4) {
        return -1;
    }

    let len = stride as usize * (height as usize - 1) + width as usize * 4;
    let data = unsafe { std::slice::from_raw_parts(pixels, len) };

    match slot().as_ref() {
        Some(backend) => {
            backend.overlay.submit(id, data, width, height, stride);
            0
        }
        None => -1,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn overlay_game_state(out: *mut GameState) -> i32 {
    if out.is_null() {
        return -1;
    }

    match slot().as_ref() {
        Some(backend) => {
            unsafe { *out = backend.overlay.game_state() };
            0
        }
        None => -1,
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_focus_game() -> i32 {
    match slot().as_ref() {
        Some(backend) => backend.overlay.focus_game() as i32,
        None => 0,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn overlay_poll_input(out: *mut InputEvent, max: i32) -> i32 {
    if out.is_null() || max <= 0 {
        return 0;
    }

    let buffer = unsafe { std::slice::from_raw_parts_mut(out, max as usize) };

    match slot().as_ref() {
        Some(backend) => backend.overlay.poll_input(buffer) as i32,
        None => 0,
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_hotkey_register(id: i32, code: u32, modifiers: u32) -> i32 {
    match slot().as_ref() {
        Some(backend) => {
            backend.input.register(id, code as u16, modifiers);
            0
        }
        None => -1,
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_hotkey_unregister(id: i32) {
    if let Some(backend) = slot().as_ref() {
        backend.input.unregister(id);
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_poll_hotkey() -> i32 {
    match slot().as_ref() {
        Some(backend) => backend.input.poll(),
        None => -1,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn overlay_key_state(out: *mut KeyState) -> i32 {
    if out.is_null() {
        return -1;
    }

    match slot().as_ref() {
        Some(backend) => {
            unsafe { *out = backend.input.snapshot() };
            0
        }
        None => -1,
    }
}
