use std::sync::{Mutex, MutexGuard};

use crate::wayland::{InputEvent, Overlay};

static OVERLAY: Mutex<Option<Overlay>> = Mutex::new(None);

fn slot() -> MutexGuard<'static, Option<Overlay>> {
    OVERLAY
        .lock()
        .unwrap_or_else(|poisoned| poisoned.into_inner())
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_init() -> i32 {
    let mut slot = slot();
    if slot.is_some() {
        return 1;
    }
    *slot = Some(Overlay::start());
    0
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_width() -> i32 {
    slot().as_ref().map_or(0, |overlay| overlay.size().0)
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_height() -> i32 {
    slot().as_ref().map_or(0, |overlay| overlay.size().1)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn overlay_submit(
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
        Some(overlay) => {
            overlay.submit(data, width, height, stride);
            0
        }
        None => -1,
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_set_visible(visible: i32) {
    if let Some(overlay) = slot().as_ref() {
        overlay.set_visible(visible != 0);
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn overlay_shutdown() {
    let overlay = slot().take();
    if let Some(overlay) = overlay {
        overlay.shutdown();
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn overlay_set_regions(rects: *const i32, count: i32) -> i32 {
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
        .map(|c| [c[0], c[1], c[2], c[3]])
        .collect();

    match slot().as_ref() {
        Some(overlay) => {
            overlay.set_regions(list);
            0
        }
        None => -1,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn overlay_poll_input(out: *mut InputEvent, max: i32) -> i32 {
    if out.is_null() || max <= 0 {
        return 0;
    }

    let buffer = unsafe { std::slice::from_raw_parts_mut(out, max as usize) };

    slot()
        .as_ref()
        .map_or(0, |overlay| overlay.poll_input(buffer) as i32)
}
