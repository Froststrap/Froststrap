use froststrap_overlay_layer::wayland::Overlay;
use std::{thread, time::Duration};

fn main() {
    let overlay = Overlay::start();
    loop {
        let (width, height) = overlay.size();
        if width > 0 && height > 0 {
            let mut pixels = vec![0u8; (width * height * 4) as usize];
            for px in pixels.chunks_exact_mut(4) {
                px.copy_from_slice(&0xA0003070u32.to_le_bytes());
            }
            overlay.submit(&pixels, width, height, width * 4);
        }
        thread::sleep(Duration::from_millis(100));
    }
}
