mod evdev_input;
mod handlers;
mod portal;
mod tracker;

use std::error::Error;
use std::sync::Arc;
use std::thread::{self, JoinHandle};
use std::time::Duration;

use crate::common::{
    InputEvent, Shared, SharedRef, SURFACE_COUNT, SURFACE_GAME, SURFACE_KEY, SURFACE_SEARCH,
};

use smithay_client_toolkit::{
    compositor::{CompositorState, Region},
    output::{OutputInfo, OutputState},
    reexports::{
        calloop::{channel, EventLoop, LoopSignal},
        calloop_wayland_source::WaylandSource,
        client::{
            globals::registry_queue_init,
            protocol::{wl_keyboard, wl_output, wl_shm},
            Connection, QueueHandle,
        },
    },
    registry::RegistryState,
    seat::{pointer::ThemedPointer, SeatState},
    shell::{
        wlr_layer::{Anchor, KeyboardInteractivity, Layer, LayerShell, LayerSurface},
        WaylandSurface,
    },
    shm::{slot::SlotPool, Shm},
};

use tracker::{SoberRect, WindowTracker, output_hash};

pub use crate::common::InputEvent as PublicInputEvent;

const TRIGGER_MOD_SHIFT: u32 = 1;
const TRIGGER_MOD_CONTROL: u32 = 4;
const TRIGGER_MOD_ALT: u32 = 8;
const TRIGGER_MOD_SUPER: u32 = 64;

fn output_size(info: &OutputInfo) -> Option<(i32, i32)> {
    if let Some((width, height)) = info.logical_size
        && width > 0
        && height > 0
    {
        return Some((width, height));
    }
    let (width, height) = info.modes.first()?.dimensions;
    let scale = info.scale_factor.max(1);
    if width <= 0 || height <= 0 {
        return None;
    }
    Some((width / scale, height / scale))
}

fn format_trigger(keysym: u32, modifiers: u32) -> String {
    let mut out = String::new();

    if modifiers & TRIGGER_MOD_CONTROL != 0 { out.push_str("<Ctrl>"); }
    if modifiers & TRIGGER_MOD_ALT != 0 { out.push_str("<Alt>"); }
    if modifiers & TRIGGER_MOD_SHIFT != 0 { out.push_str("<Shift>"); }
    if modifiers & TRIGGER_MOD_SUPER != 0 { out.push_str("<Super>"); }

    let key = match keysym {
        0xFF0D => "Return".to_string(),
        0xFF1B => "Escape".to_string(),
        0xFF09 => "Tab".to_string(),
        0xFF08 => "BackSpace".to_string(),
        0x20 => "space".to_string(),
        0xFF52 => "Up".to_string(),
        0xFF54 => "Down".to_string(),
        0xFF51 => "Left".to_string(),
        0xFF53 => "Right".to_string(),
        0xFFBE..=0xFFC9 => format!("F{}", keysym - 0xFFBE + 1),
        k if (0x61..=0x7A).contains(&k) => (k as u8 as char).to_string(),
        k if (0x30..=0x39).contains(&k) => (k as u8 as char).to_string(),
        k => format!("0x{k:x}"),
    };

    out.push_str(&key);
    out
}

const SURFACE_NAMES: [&str; SURFACE_COUNT] =
    ["froststrap-overlay", "froststrap-keys", "froststrap-search"];

const SURFACE_INTERACTIVITY: [KeyboardInteractivity; SURFACE_COUNT] = [
    KeyboardInteractivity::OnDemand,
    KeyboardInteractivity::None,
    KeyboardInteractivity::OnDemand,
];

enum Command {
    Present(usize),
    Sync,
    SetSurfaceRect(usize, i32, i32, i32, i32),
    SetVisible(usize, bool),
    SetRegions(usize, Vec<[i32; 4]>),
    ClearRegions(usize),
    FocusGame,
    Shutdown,
}

pub struct Overlay {
    shared: SharedRef,
    sender: channel::Sender<Command>,
    thread: Option<JoinHandle<()>>,
    process_id: i32,
}

impl Overlay {
    pub fn start(process_id: i32) -> Result<Overlay, String> {
        let shared = Arc::new(Shared::new());
        let (sender, receiver) = channel::channel::<Command>();
        let (ready_tx, ready_rx) = std::sync::mpsc::sync_channel::<Result<(), String>>(1);

        let thread_shared = shared.clone();
        let thread_sender = sender.clone();
        let thread = thread::spawn(move || {
            match setup(process_id, &thread_shared, &thread_sender, receiver) {
                Ok((mut app, mut event_loop)) => {
                    let _ = ready_tx.send(Ok(()));
                    if let Err(err) = event_loop.run(None, &mut app, |_| {}) {
                        eprintln!("overlay stopped: {err}");
                    }
                }
                Err(err) => {
                    let _ = ready_tx.send(Err(err.to_string()));
                }
            }
        });

        match ready_rx.recv_timeout(Duration::from_secs(5)) {
            Ok(Ok(())) => Ok(Overlay { shared, sender, thread: Some(thread), process_id }),
            Ok(Err(err)) => {
                let _ = thread.join();
                Err(err)
            }
            Err(_) => {
                let _ = sender.send(Command::Shutdown);
                Err("timed out waiting for the Wayland compositor".to_string())
            }
        }
    }

    pub fn size(&self, surface: usize) -> (i32, i32) { self.shared.size(surface) }

    pub fn scale(&self, surface: usize) -> i32 { self.shared.scale(surface) }

    pub fn game_rect(&self) -> crate::common::GameRect { self.shared.rect() }

    pub fn focus_known(&self) -> bool { self.shared.focus_known() }

    pub fn is_foreground(&self, pid: i32) -> bool {
        if pid == self.process_id {
            self.shared.foreground() == pid
        } else {
            self.shared.own_focus()
        }
    }

    pub fn submit(&self, surface: usize, pixels: &[u8], width: i32, height: i32, stride: i32) {
        self.shared.submit(surface, pixels, width, height, stride);
        let _ = self.sender.send(Command::Present(surface));
    }

    pub fn set_visible(&self, surface: usize, visible: bool) {
        let _ = self.sender.send(Command::SetVisible(surface, visible));
    }

    pub fn set_regions(&self, surface: usize, rects: Vec<[i32; 4]>) {
        let _ = self.sender.send(Command::SetRegions(surface, rects));
    }

    pub fn clear_regions(&self, surface: usize) {
        let _ = self.sender.send(Command::ClearRegions(surface));
    }

    pub fn set_surface_rect(&self, surface: usize, x: i32, y: i32, width: i32, height: i32) {
        let _ = self.sender.send(Command::SetSurfaceRect(surface, x, y, width, height));
    }

    pub fn focus_game(&self) { let _ = self.sender.send(Command::FocusGame); }

    pub fn poll_input(&self, out: &mut [InputEvent]) -> usize { self.shared.poll_input(out) }

    pub fn register_hotkey(&self, id: i32, keysym: u32, evdev_keycode: u32, modifiers: u32) {
        let trigger = format_trigger(keysym, modifiers);
        portal::request(id, &format!("Froststrap {id}"), &trigger);
        evdev_input::register_hotkey(id, evdev_keycode, modifiers);
    }

    pub fn unregister_hotkey(&self, id: i32) {
        portal::unregister(id);
        evdev_input::unregister_hotkey(id);
    }

    pub fn set_key_monitoring(&self, enabled: bool) {
        evdev_input::set_enabled(enabled);
    }

    pub fn foreground(&self) -> i32 {
        self.shared.foreground()
    }

    pub fn minimised(&self) -> bool {
        self.shared.minimised()
    }

    pub fn shutdown(mut self) {
        let _ = self.sender.send(Command::Shutdown);
        if let Some(thread) = self.thread.take() {
            let _ = thread.join();
        }
    }
}

struct Surface {
    layer: LayerSurface,
    pool: SlotPool,
    rect: Option<SoberRect>,
    output: Option<wl_output::WlOutput>,
    origin: (i32, i32),
    scale: i32,
    configured: bool,
    visible: bool,
}

impl Surface {
    fn draw(&mut self, shared: &Shared, index: usize) {
        let Some(rect) = self.rect else { return };
        if !self.configured || !self.visible {
            return;
        }
        let scale = self.scale.max(1);
        let width = rect.width.max(1).saturating_mul(scale);
        let height = rect.height.max(1).saturating_mul(scale);
        let Ok((buffer, canvas)) = self.pool.create_buffer(
            width, height, width * 4, wl_shm::Format::Argb8888,
        ) else {
            return;
        };
        canvas.fill(0);
        shared.copy_frame_into(index, canvas, width as usize * 4, width, height);
        self.layer.wl_surface().set_buffer_scale(scale);
        self.layer.wl_surface().damage_buffer(0, 0, width, height);
        if buffer.attach_to(self.layer.wl_surface()).is_ok() {
            self.layer.commit();
        }
    }
}

struct App {
    registry_state: RegistryState,
    output_state: OutputState,
    entered_output: Option<wl_output::WlOutput>,
    shm: Shm,
    surfaces: Vec<Surface>,
    tracker: Box<dyn WindowTracker>,
    shared: SharedRef,
    signal: LoopSignal,
    compositor: CompositorState,
    layer_shell: LayerShell,
    qh: QueueHandle<App>,
    seat_state: SeatState,
    pointer: Option<ThemedPointer>,
    keyboard: Option<wl_keyboard::WlKeyboard>,
}

impl App {
    fn output_rect(&self, hash: i64) -> Option<SoberRect> {
        for output in self.output_state.outputs() {
            let Some(info) = self.output_state.info(&output) else { continue };
            let Some(name) = info.name.as_deref() else { continue };
            if output_hash(name) != hash {
                continue;
            }
            let Some(origin) = info.logical_position else { continue };
            let Some((width, height)) = output_size(&info) else { continue };
            return Some(SoberRect { monitor: hash, x: origin.0, y: origin.1, width, height });
        }
        None
    }

    fn game_rect(&self) -> Option<SoberRect> {
        if let Some(rect) = self.tracker.query() {
            return Some(rect);
        }
        if !self.tracker.uses_output() {
            return None;
        }
        if let Some(hash) = self.tracker.game_output()
            && let Some(rect) = self.output_rect(hash)
        {
            return Some(rect);
        }
        if let Some(output) = self.entered_output.as_ref()
            && let Some(info) = self.output_state.info(output)
            && let Some((width, height)) = output_size(&info)
        {
            let origin = info.logical_position.unwrap_or((0, 0));
            return Some(SoberRect { monitor: 0, x: origin.0, y: origin.1, width, height });
        }
        let mut best: Option<SoberRect> = None;
        for output in self.output_state.outputs() {
            let Some(info) = self.output_state.info(&output) else { continue };
            let Some((width, height)) = output_size(&info) else { continue };
            let rect = SoberRect { monitor: 0, x: 0, y: 0, width, height };
            let area = i64::from(width) * i64::from(height);
            if best.map(|c| area > i64::from(c.width) * i64::from(c.height)).unwrap_or(true) {
                best = Some(rect);
            }
        }
        best
    }

    fn output_for(&self, rect: &SoberRect) -> Option<(wl_output::WlOutput, (i32, i32), i32)> {
        let mut best: Option<(wl_output::WlOutput, (i32, i32), i32, i64)> = None;

        for output in self.output_state.outputs() {
            let Some(info) = self.output_state.info(&output) else { continue };
            let Some(origin) = info.logical_position else { continue };
            let Some((width, height)) = output_size(&info) else { continue };

            let left = rect.x.max(origin.0);
            let top = rect.y.max(origin.1);
            let right = (rect.x + rect.width).min(origin.0 + width);
            let bottom = (rect.y + rect.height).min(origin.1 + height);
            if right <= left || bottom <= top {
                continue;
            }

            let area = i64::from(right - left) * i64::from(bottom - top);
            if best.as_ref().map(|candidate| area > candidate.3).unwrap_or(true) {
                best = Some((output, origin, info.scale_factor.max(1), area));
            }
        }

        if let Some((output, origin, scale, _)) = best {
            return Some((output, origin, scale));
        }

        if rect.monitor != 0 {
            for output in self.output_state.outputs() {
                let Some(info) = self.output_state.info(&output) else { continue };
                let Some(origin) = info.logical_position else { continue };
                let Some((width, height)) = output_size(&info) else { continue };
                let Some(name) = info.name.as_deref() else { continue };
                if output_hash(name) != rect.monitor {
                    continue;
                }
                if rect.x < 0
                    || rect.y < 0
                    || rect.x + rect.width > width
                    || rect.y + rect.height > height
                {
                    continue;
                }
                return Some((output, origin, info.scale_factor.max(1)));
            }
        }

        let mut fallback: Option<(wl_output::WlOutput, (i32, i32), i32, i64)> = None;
        for output in self.output_state.outputs() {
            let Some(info) = self.output_state.info(&output) else { continue };
            let Some(origin) = info.logical_position else { continue };
            let Some((width, height)) = output_size(&info) else { continue };
            let area = i64::from(width) * i64::from(height);
            if fallback.as_ref().map(|candidate| area > candidate.3).unwrap_or(true) {
                fallback = Some((output, origin, info.scale_factor.max(1), area));
            }
        }

        fallback.map(|(output, origin, scale, _)| (output, origin, scale))
    }

    fn place(&mut self, index: usize, rect: SoberRect) {
        let (output, origin, scale) = match self.output_for(&rect) {
            Some((output, origin, scale)) => (Some(output), origin, scale),
            None => (None, (0, 0), 1),
        };

        let surface = &self.surfaces[index];
        let unchanged = surface.rect == Some(rect)
            && surface.output.as_ref() == output.as_ref()
            && surface.origin == origin
            && surface.scale == scale;
        if unchanged {
            return;
        }

        self.shared.set_scale(index, scale);

        let moved = self.surfaces[index].output.as_ref() != output.as_ref();
        if moved {
            let wl_surface = self.compositor.create_surface(&self.qh);
            let layer = self.layer_shell.create_layer_surface(
                &self.qh,
                wl_surface,
                Layer::Overlay,
                Some(SURFACE_NAMES[index]),
                output.as_ref(),
            );
            layer.set_anchor(Anchor::TOP | Anchor::LEFT);
            layer.set_exclusive_zone(-1);
            layer.set_keyboard_interactivity(SURFACE_INTERACTIVITY[index]);
            if let Ok(empty) = Region::new(&self.compositor) {
                layer.wl_surface().set_input_region(Some(empty.wl_region()));
            }

            let old = std::mem::replace(&mut self.surfaces[index].layer, layer);
            let old_surface = old.wl_surface().clone();
            drop(old);
            old_surface.destroy();

            self.surfaces[index].output = output;
            self.surfaces[index].configured = false;
        }

        let surface = &mut self.surfaces[index];
        if surface.scale != scale {
            surface.scale = scale;
            surface.layer.wl_surface().set_buffer_scale(scale);
        }
        surface.rect = Some(rect);
        surface.origin = origin;
        surface.layer.set_size(rect.width.max(1) as u32, rect.height.max(1) as u32);
        surface.layer.set_margin((rect.y - origin.1).max(0), 0, 0, (rect.x - origin.0).max(0));
        surface.layer.commit();
    }

    fn sync_tracking(&mut self) {
        let foreground = self.tracker.foreground();
        let minimised = self.tracker.minimised();
        self.shared.set_foreground(foreground);
        self.shared.set_minimised(minimised);
        self.shared.set_focus_known(self.tracker.focus_known());

        let next = self.game_rect();
        self.shared.set_rect(match next {
            Some(rect) => crate::common::GameRect {
                x: rect.x, y: rect.y, width: rect.width, height: rect.height, valid: 1,
            },
            None => crate::common::GameRect::default(),
        });

        match next {
            Some(rect) => {
                self.shared.set_size(SURFACE_GAME, rect.width, rect.height);
                self.place(SURFACE_GAME, rect);
            }
            None => {
                if self.surfaces[SURFACE_GAME].rect.is_some() {
                    self.surfaces[SURFACE_GAME].rect = None;
                    self.shared.set_size(SURFACE_GAME, 0, 0);
                    self.surfaces[SURFACE_GAME].configured = false;
                    let layer = &self.surfaces[SURFACE_GAME].layer;
                    layer.wl_surface().attach(None, 0, 0);
                    layer.commit();
                }
            }
        }
    }

    fn set_surface_rect(&mut self, index: usize, x: i32, y: i32, width: i32, height: i32) {
        if index >= self.surfaces.len() || index == SURFACE_GAME {
            return;
        }
        let rect = SoberRect { monitor: 0, x, y, width: width.max(1), height: height.max(1) };
        self.shared.set_size(index, rect.width, rect.height);
        self.place(index, rect);
    }

    fn set_regions(&mut self, index: usize, rects: Vec<[i32; 4]>) {
        let Some(surface) = self.surfaces.get(index) else { return };
        let Ok(region) = Region::new(&self.compositor) else { return };
        for [x, y, w, h] in rects {
            region.add(x, y, w, h);
        }
        surface.layer.wl_surface().set_input_region(Some(region.wl_region()));
        surface.layer.commit();
    }

    fn clear_regions(&mut self, index: usize) {
        let Some(surface) = self.surfaces.get(index) else { return };
        surface.layer.wl_surface().set_input_region(None);
        surface.layer.commit();
    }

    fn set_visible(&mut self, index: usize, visible: bool) {
        let Some(surface) = self.surfaces.get_mut(index) else { return };
        surface.visible = visible;
        if visible {
            surface.layer.wl_surface().commit();
        } else {
            surface.layer.wl_surface().attach(None, 0, 0);
            surface.layer.commit();
        }
    }

    fn handle(&mut self, command: Command) {
        match command {
            Command::Present(index) => {
                if let Some(surface) = self.surfaces.get_mut(index) {
                    surface.draw(&self.shared, index);
                }
            }
            Command::Sync => self.sync_tracking(),
            Command::SetSurfaceRect(index, x, y, w, h) => self.set_surface_rect(index, x, y, w, h),
            Command::SetVisible(index, visible) => self.set_visible(index, visible),
            Command::SetRegions(index, rects) => self.set_regions(index, rects),
            Command::ClearRegions(index) => self.clear_regions(index),
            Command::FocusGame => self.tracker.focus(),
            Command::Shutdown => self.signal.stop(),
        }
    }
}

fn setup(
    process_id: i32,
    shared: &SharedRef,
    sender: &channel::Sender<Command>,
    receiver: channel::Channel<Command>,
) -> Result<(App, EventLoop<'static, App>), Box<dyn Error>> {
    let conn = Connection::connect_to_env()?;
    let (globals, event_queue) = registry_queue_init(&conn)?;
    let qh = event_queue.handle();

    let compositor = CompositorState::bind(&globals, &qh)?;
    let layer_shell = LayerShell::bind(&globals, &qh)?;
    let shm = Shm::bind(&globals, &qh)?;

    let tracker = tracker::detect(process_id).ok_or("no supported compositor detected")?;

    let names = SURFACE_NAMES;
    let interactivity = SURFACE_INTERACTIVITY;

    let mut surfaces = Vec::with_capacity(SURFACE_COUNT);
    for index in 0..SURFACE_COUNT {
        let surface = compositor.create_surface(&qh);
        let layer = layer_shell.create_layer_surface(
            &qh, surface, Layer::Overlay, Some(names[index]), None,
        );
        layer.set_anchor(Anchor::TOP | Anchor::LEFT);
        layer.set_exclusive_zone(-1);
        layer.set_keyboard_interactivity(interactivity[index]);
        let empty = Region::new(&compositor)?;
        layer.wl_surface().set_input_region(Some(empty.wl_region()));
        layer.set_size(1, 1);
        layer.commit();
        let pool = SlotPool::new(8192, &shm)?;
        surfaces.push(Surface {
            layer, pool, rect: None, output: None, origin: (0, 0), scale: 1, configured: false, visible: true,
        });
    }

    let _ = (SURFACE_KEY, SURFACE_SEARCH);

    let event_loop: EventLoop<App> = EventLoop::try_new()?;
    let handle = event_loop.handle();

    WaylandSource::new(conn.clone(), event_queue)
        .insert(handle.clone())
        .map_err(|_| "failed to register the wayland source")?;

    handle
        .insert_source(receiver, |event, _, app| match event {
            channel::Event::Msg(command) => app.handle(command),
            channel::Event::Closed => app.signal.stop(),
        })
        .map_err(|_| "failed to register the command channel")?;

    if let Some(events) = tracker.watch() {
        let sender = sender.clone();
        thread::spawn(move || {
            while events.recv().is_ok() {
                if sender.send(Command::Sync).is_err() {
                    break;
                }
            }
        });
    }

    let mut app = App {
        registry_state: RegistryState::new(&globals),
        output_state: OutputState::new(&globals, &qh),
        entered_output: None,
        shm,
        surfaces,
        tracker,
        shared: shared.clone(),
        signal: event_loop.get_signal(),
        compositor,
        layer_shell,
        qh: qh.clone(),
        seat_state: SeatState::new(&globals, &qh),
        pointer: None,
        keyboard: None,
    };

    let sender_tick = sender.clone();
    handle
        .insert_source(
            smithay_client_toolkit::reexports::calloop::timer::Timer::from_duration(Duration::from_millis(100)),
            move |_, _, _| {
                let _ = sender_tick.send(Command::Sync);
                smithay_client_toolkit::reexports::calloop::timer::TimeoutAction::ToDuration(Duration::from_millis(100))
            },
        )
        .map_err(|_| "failed to register tracking timer")?;

    app.handle(Command::Sync);

    portal::start(shared.clone());
    evdev_input::start(shared.clone());

    Ok((app, event_loop))
}