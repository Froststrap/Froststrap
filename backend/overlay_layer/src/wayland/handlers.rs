use smithay_client_toolkit::{
    compositor::CompositorHandler,
    delegate_compositor, delegate_keyboard, delegate_layer, delegate_output, delegate_pointer,
    delegate_registry, delegate_seat, delegate_shm,
    output::{OutputHandler, OutputState},
    reexports::client::{
        protocol::{wl_keyboard, wl_output, wl_pointer, wl_seat, wl_surface},
        Connection, QueueHandle,
    },
    registry::{ProvidesRegistryState, RegistryState},
    registry_handlers,
    seat::{
        keyboard::{KeyEvent, KeyboardHandler, Keysym, Modifiers},
        pointer::{CursorIcon, PointerEvent, PointerEventKind, PointerHandler, ThemeSpec},
        Capability, SeatHandler, SeatState,
    },
    shell::{
        wlr_layer::{LayerShellHandler, LayerSurface, LayerSurfaceConfigure},
        WaylandSurface,
    },
    shm::{Shm, ShmHandler},
};

use crate::common::{
    InputEvent, KIND_AXIS, KIND_BUTTON_DOWN, KIND_BUTTON_UP, KIND_ENTER, KIND_KEY_DOWN, KIND_KEY_UP,
    KIND_LEAVE, KIND_MOTION, KIND_TEXT,
};

use super::App;

impl CompositorHandler for App {
    fn scale_factor_changed(&mut self, _: &Connection, _: &QueueHandle<Self>, _: &wl_surface::WlSurface, _: i32) {}
    fn transform_changed(&mut self, _: &Connection, _: &QueueHandle<Self>, _: &wl_surface::WlSurface, _: wl_output::Transform) {}
    fn frame(&mut self, _: &Connection, _: &QueueHandle<Self>, _: &wl_surface::WlSurface, _: u32) {}
    fn surface_enter(&mut self, _: &Connection, _: &QueueHandle<Self>, _: &wl_surface::WlSurface, output: &wl_output::WlOutput) {
        self.entered_output = Some(output.clone());
    }
    fn surface_leave(&mut self, _: &Connection, _: &QueueHandle<Self>, _: &wl_surface::WlSurface, output: &wl_output::WlOutput) {
        if self.entered_output.as_ref() == Some(output) {
            self.entered_output = None;
        }
    }
}

impl OutputHandler for App {
    fn output_state(&mut self) -> &mut OutputState { &mut self.output_state }
    fn new_output(&mut self, _: &Connection, _: &QueueHandle<Self>, _: wl_output::WlOutput) {}
    fn update_output(&mut self, _: &Connection, _: &QueueHandle<Self>, _: wl_output::WlOutput) {}
    fn output_destroyed(&mut self, _: &Connection, _: &QueueHandle<Self>, _: wl_output::WlOutput) {}
}

impl LayerShellHandler for App {
    fn closed(&mut self, _: &Connection, _: &QueueHandle<Self>, _: &LayerSurface) {
        self.signal.stop();
    }
    fn configure(&mut self, _: &Connection, _: &QueueHandle<Self>, layer: &LayerSurface, _: LayerSurfaceConfigure, _: u32) {
        let Some(index) = self
            .surfaces
            .iter()
            .position(|surface| surface.layer.wl_surface() == layer.wl_surface())
        else {
            return;
        };

        self.surfaces[index].configured = true;
        let shared = self.shared.clone();
        self.surfaces[index].draw(&shared, index);
    }
}

impl ShmHandler for App {
    fn shm_state(&mut self) -> &mut Shm { &mut self.shm }
}

impl ProvidesRegistryState for App {
    fn registry(&mut self) -> &mut RegistryState { &mut self.registry_state }
    registry_handlers![OutputState, SeatState];
}

impl SeatHandler for App {
    fn seat_state(&mut self) -> &mut SeatState { &mut self.seat_state }

    fn new_seat(&mut self, _: &Connection, _: &QueueHandle<Self>, _: wl_seat::WlSeat) {}

    fn new_capability(&mut self, _: &Connection, qh: &QueueHandle<Self>, seat: wl_seat::WlSeat, capability: Capability) {
        if capability == Capability::Pointer && self.pointer.is_none() {
            let cursor = self.compositor.create_surface(qh);
            self.pointer = self
                .seat_state
                .get_pointer_with_theme(qh, &seat, self.shm.wl_shm(), cursor, ThemeSpec::System)
                .ok();
        }
        if capability == Capability::Keyboard && self.keyboard.is_none() {
            self.keyboard = self.seat_state.get_keyboard(qh, &seat, None).ok();
        }
    }

    fn remove_capability(&mut self, _: &Connection, _: &QueueHandle<Self>, _: wl_seat::WlSeat, capability: Capability) {
        if capability == Capability::Pointer {
            if let Some(themed) = self.pointer.take() {
                themed.pointer().release();
            }
        }
        if capability == Capability::Keyboard {
            if let Some(keyboard) = self.keyboard.take() {
                keyboard.release();
            }
        }
    }

    fn remove_seat(&mut self, _: &Connection, _: &QueueHandle<Self>, _: wl_seat::WlSeat) {}
}

impl PointerHandler for App {
    fn pointer_frame(
        &mut self,
        conn: &Connection,
        _qh: &QueueHandle<Self>,
        _pointer: &wl_pointer::WlPointer,
        events: &[PointerEvent],
    ) {
        for event in events {
            let mut out = InputEvent {
                x: event.position.0 as f32,
                y: event.position.1 as f32,
                ..InputEvent::default()
            };
            match event.kind {
                PointerEventKind::Enter { .. } => {
                    out.kind = KIND_ENTER;
                    if let Some(themed) = self.pointer.as_ref() {
                        let _ = themed.set_cursor(conn, CursorIcon::Default);
                    }
                }
                PointerEventKind::Leave { .. } => out.kind = KIND_LEAVE,
                PointerEventKind::Motion { .. } => out.kind = KIND_MOTION,
                PointerEventKind::Press { button, .. } => {
                    out.kind = KIND_BUTTON_DOWN;
                    out.code = button;
                }
                PointerEventKind::Release { button, .. } => {
                    out.kind = KIND_BUTTON_UP;
                    out.code = button;
                }
                PointerEventKind::Axis { horizontal, vertical, .. } => {
                    out.kind = KIND_AXIS;
                    out.dx = horizontal.absolute as f32;
                    out.dy = vertical.absolute as f32;
                }
            }
            self.shared.push_input(out);
        }
    }
}

impl KeyboardHandler for App {
    fn enter(&mut self, _: &Connection, _: &QueueHandle<Self>, _: &wl_keyboard::WlKeyboard, _: &wl_surface::WlSurface, _: u32, _: &[u32], _: &[Keysym]) {
        self.shared.set_own_focus(true);
    }

    fn leave(&mut self, _: &Connection, _: &QueueHandle<Self>, _: &wl_keyboard::WlKeyboard, _: &wl_surface::WlSurface, _: u32) {
        self.shared.set_own_focus(false);
    }

    fn press_key(&mut self, _: &Connection, _: &QueueHandle<Self>, _: &wl_keyboard::WlKeyboard, _: u32, event: KeyEvent) {
        self.shared.push_input(InputEvent {
            kind: KIND_KEY_DOWN,
            code: event.raw_code,
            keysym: event.keysym.into(),
            ..InputEvent::default()
        });

        if let Some(text) = event.utf8
            && !text.is_empty()
            && !text.chars().any(char::is_control)
        {
            self.shared.push_input(
                InputEvent { kind: KIND_TEXT, ..InputEvent::default() }.with_text(&text),
            );
        }
    }

    fn release_key(&mut self, _: &Connection, _: &QueueHandle<Self>, _: &wl_keyboard::WlKeyboard, _: u32, event: KeyEvent) {
        self.shared.push_input(InputEvent {
            kind: KIND_KEY_UP,
            code: event.raw_code,
            keysym: event.keysym.into(),
            ..InputEvent::default()
        });
    }

    fn update_modifiers(&mut self, _: &Connection, _: &QueueHandle<Self>, _: &wl_keyboard::WlKeyboard, _: u32, _: Modifiers, _: u32) {}
}

delegate_seat!(App);
delegate_pointer!(App);
delegate_keyboard!(App);
delegate_compositor!(App);
delegate_output!(App);
delegate_shm!(App);
delegate_layer!(App);
delegate_registry!(App);