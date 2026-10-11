fn main() {
    if let Err(err) = froststrap_overlay_layer::wayland::run_rect_demo() {
        eprintln!("overlay failed: {err}");
        std::process::exit(1);
    }
}
