// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

mod handlers;
mod tracker;

use std::collections::{HashMap, VecDeque};
use std::error::Error;
use std::sync::atomic::{AtomicBool, AtomicI32, Ordering};
use std::sync::{Arc, Mutex, MutexGuard};
use std::thread::{self, JoinHandle};
use std::time::Duration;

use smithay_client_toolkit::{
    compositor::{CompositorState, Region},
    output::OutputState,
    reexports::{
        calloop::{EventLoop, LoopSignal, channel},
        calloop_wayland_source::WaylandSource,
        client::{
            Connection, QueueHandle,
            globals::registry_queue_init,
            protocol::{wl_keyboard, wl_shm, wl_surface},
        },
    },
    registry::RegistryState,
    seat::{SeatState, pointer::ThemedPointer},
    shell::{
        WaylandSurface,
        wlr_layer::{Anchor, KeyboardInteractivity, Layer, LayerShell, LayerSurface},
    },
    shm::{Shm, slot::SlotPool},
};

fn lock<T>(mutex: &Mutex<T>) -> MutexGuard<'_, T> {
    mutex
        .lock()
        .unwrap_or_else(|poisoned| poisoned.into_inner())
}

struct Frame {
    width: i32,
    height: i32,
    pixels: Vec<u8>,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct InputEvent {
    pub surface: i32,
    pub kind: i32,
    pub code: u32,
    pub modifiers: u32,
    pub x: f32,
    pub y: f32,
    pub dx: f32,
    pub dy: f32,
    pub text: [u8; 32],
}

impl Default for InputEvent {
    fn default() -> Self {
        InputEvent {
            surface: -1,
            kind: 0,
            code: 0,
            modifiers: 0,
            x: 0.0,
            y: 0.0,
            dx: 0.0,
            dy: 0.0,
            text: [0; 32],
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct GameState {
    pub x: i32,
    pub y: i32,
    pub width: i32,
    pub height: i32,
    pub flags: u32,
}

pub const GAME_PRESENT: u32 = 1;
pub const GAME_VISIBLE: u32 = 2;
pub const GAME_FOREGROUND: u32 = 4;
pub const GAME_MINIMISED: u32 = 8;
pub const GAME_MAXIMISED: u32 = 16;

impl GameState {
    fn from_info(info: &tracker::GameInfo) -> GameState {
        let mut flags = 0;

        if info.present {
            flags |= GAME_PRESENT;
        }

        if info.visible {
            flags |= GAME_VISIBLE;
        }

        if info.foreground {
            flags |= GAME_FOREGROUND;
        }

        if info.present && !info.visible {
            flags |= GAME_MINIMISED;
        }

        if info.fullscreen {
            flags |= GAME_MAXIMISED;
        }

        match info.rect {
            Some(rect) => GameState {
                x: rect.x,
                y: rect.y,
                width: rect.width,
                height: rect.height,
                flags,
            },
            None => GameState {
                x: 0,
                y: 0,
                width: 0,
                height: 0,
                flags,
            },
        }
    }
}

struct Shared {
    frames: Mutex<HashMap<i32, Frame>>,
    input: Mutex<VecDeque<InputEvent>>,
    game: Mutex<GameState>,
}

enum Command {
    Create { id: i32, tracked: bool },
    Destroy(i32),
    SetVisible { id: i32, visible: bool },
    SetRect { id: i32, x: i32, y: i32, width: i32, height: i32 },
    SetKeyboard { id: i32, mode: i32 },
    SetRegions { id: i32, rects: Vec<[i32; 4]> },
    Present(i32),
    Sync,
    Shutdown,
}

pub struct Overlay {
    shared: Arc<Shared>,
    sender: channel::Sender<Command>,
    thread: Option<JoinHandle<()>>,
    poll_thread: Option<JoinHandle<()>>,
    stop: Arc<AtomicBool>,
    next_id: AtomicI32,
}

impl Overlay {
    pub fn start() -> Overlay {
        let shared = Arc::new(Shared {
            frames: Mutex::new(HashMap::new()),
            input: Mutex::new(VecDeque::new()),
            game: Mutex::new(GameState::default()),
        });
        let (sender, receiver) = channel::channel::<Command>();
        let stop = Arc::new(AtomicBool::new(false));

        let run_shared = shared.clone();
        let thread = thread::spawn(move || {
            if let Err(err) = run(run_shared, receiver) {
                eprintln!("overlay stopped: {err}");
            }
        });

        let poll_shared = shared.clone();
        let poll_sender = sender.clone();
        let poll_stop = stop.clone();
        let poll_thread = thread::Builder::new()
            .name("froststrap-overlay-track".into())
            .spawn(move || poll_game(poll_shared, poll_sender, poll_stop))
            .ok();

        Overlay {
            shared,
            sender,
            thread: Some(thread),
            poll_thread,
            stop,
            next_id: AtomicI32::new(0),
        }
    }

    pub fn create_surface(&self, tracked: bool) -> i32 {
        let id = self.next_id.fetch_add(1, Ordering::Relaxed);
        let _ = self.sender.send(Command::Create { id, tracked });
        id
    }

    pub fn destroy_surface(&self, id: i32) {
        let _ = self.sender.send(Command::Destroy(id));
    }

    pub fn set_visible(&self, id: i32, visible: bool) {
        let _ = self.sender.send(Command::SetVisible { id, visible });
    }

    pub fn set_rect(&self, id: i32, x: i32, y: i32, width: i32, height: i32) {
        let _ = self.sender.send(Command::SetRect {
            id,
            x,
            y,
            width,
            height,
        });
    }

    pub fn set_keyboard(&self, id: i32, mode: i32) {
        let _ = self.sender.send(Command::SetKeyboard { id, mode });
    }

    pub fn set_regions(&self, id: i32, rects: Vec<[i32; 4]>) {
        let _ = self.sender.send(Command::SetRegions { id, rects });
    }

    pub fn submit(&self, id: i32, pixels: &[u8], width: i32, height: i32, stride: i32) {
        if width <= 0 || height <= 0 || stride < width.saturating_mul(4) {
            return;
        }

        let row_bytes = width as usize * 4;
        let needed = stride as usize * (height as usize - 1) + row_bytes;
        if pixels.len() < needed {
            return;
        }

        {
            let mut frames = lock(&self.shared.frames);
            let frame = frames.entry(id).or_insert_with(|| Frame {
                width: 0,
                height: 0,
                pixels: Vec::new(),
            });
            frame.width = width;
            frame.height = height;
            frame.pixels.resize(row_bytes * height as usize, 0);

            if stride as usize == row_bytes {
                frame
                    .pixels
                    .copy_from_slice(&pixels[..row_bytes * height as usize]);
            } else {
                for row in 0..height as usize {
                    let src = row * stride as usize;
                    let dst = row * row_bytes;
                    frame.pixels[dst..dst + row_bytes].copy_from_slice(&pixels[src..src + row_bytes]);
                }
            }
        }

        let _ = self.sender.send(Command::Present(id));
    }

    pub fn game_state(&self) -> GameState {
        *lock(&self.shared.game)
    }

    pub fn focus_game(&self) -> bool {
        tracker::detect()
            .map(|tracker| tracker.focus_game())
            .unwrap_or(false)
    }

    pub fn poll_input(&self, out: &mut [InputEvent]) -> usize {
        let mut queue = lock(&self.shared.input);
        let mut count = 0;
        while count < out.len() {
            match queue.pop_front() {
                Some(event) => {
                    out[count] = event;
                    count += 1;
                }
                None => break,
            }
        }
        count
    }

    pub fn shutdown(mut self) {
        self.stop.store(true, Ordering::Relaxed);
        let _ = self.sender.send(Command::Shutdown);

        if let Some(thread) = self.thread.take() {
            let _ = thread.join();
        }

        if let Some(thread) = self.poll_thread.take() {
            let _ = thread.join();
        }
    }
}

fn poll_game(shared: Arc<Shared>, sender: channel::Sender<Command>, stop: Arc<AtomicBool>) {
    let Some(tracker) = tracker::detect() else {
        return;
    };

    while !stop.load(Ordering::Relaxed) {
        let state = GameState::from_info(&tracker.game_info());

        let changed = {
            let mut game = lock(&shared.game);
            let changed = *game != state;
            *game = state;
            changed
        };

        if changed && sender.send(Command::Sync).is_err() {
            return;
        }

        thread::sleep(Duration::from_millis(33));
    }
}

struct Surface {
    layer: LayerSurface,
    tracked: bool,
    visible: bool,
    configured: bool,
    x: i32,
    y: i32,
    width: i32,
    height: i32,
}

impl Surface {
    fn apply_game(&mut self, game: GameState) {
        if game.flags & GAME_PRESENT != 0 {
            self.set_geometry(game.x, game.y, game.width, game.height);
        } else {
            self.set_geometry(0, 0, 1, 1);
        }
    }

    fn set_geometry(&mut self, x: i32, y: i32, width: i32, height: i32) {
        let width = width.max(1);
        let height = height.max(1);

        if self.x == x && self.y == y && self.width == width && self.height == height {
            return;
        }

        self.x = x;
        self.y = y;
        self.width = width;
        self.height = height;

        self.layer.set_size(width as u32, height as u32);
        self.layer.set_margin(y, 0, 0, x);
        self.layer.commit();
    }
}

struct App {
    registry_state: RegistryState,
    output_state: OutputState,
    compositor: CompositorState,
    seat_state: SeatState,
    layer_shell: LayerShell,
    shm: Shm,
    pool: SlotPool,
    shared: Arc<Shared>,
    signal: LoopSignal,
    qh: QueueHandle<App>,
    surfaces: HashMap<i32, Surface>,
    pointer: Option<ThemedPointer>,
    keyboard: Option<wl_keyboard::WlKeyboard>,
    keyboard_surface: Option<i32>,
    modifiers: u32,
}

impl App {
    fn surface_id(&self, surface: &wl_surface::WlSurface) -> Option<i32> {
        self.surfaces
            .iter()
            .find(|(_, value)| value.layer.wl_surface() == surface)
            .map(|(id, _)| *id)
    }

    fn push_input(&self, event: InputEvent) {
        let mut queue = lock(&self.shared.input);
        if queue.len() >= 4096 {
            queue.pop_front();
        }
        queue.push_back(event);
    }

    fn draw(&mut self, id: i32) {
        {
            let Some(surface) = self.surfaces.get(&id) else {
                log::warn!("wayland: draw id={id} skipped — surface missing");
                return;
            };

            if !surface.configured {
                log::warn!("wayland: draw id={id} skipped — not configured yet");
                return;
            }

            if !surface.visible {
                log::debug!("wayland: draw id={id} skipped — not visible");
                return;
            }
        }

        let (width, height) = {
            let surface = &self.surfaces[&id];
            (surface.width.max(1), surface.height.max(1))
        };

        let Ok((buffer, canvas)) =
            self.pool
                .create_buffer(width, height, width * 4, wl_shm::Format::Argb8888)
        else {
            return;
        };

        canvas.fill(0);

        {
            let frames = lock(&self.shared.frames);

            match frames.get(&id) {
                Some(frame) => {
                    let copy_width = frame.width.min(width).max(0) as usize;
                    let copy_height = frame.height.min(height).max(0) as usize;
                    let src_stride = frame.width as usize * 4;
                    let dst_stride = width as usize * 4;

                    if src_stride > 0 && copy_height > 0 && frame.pixels.len() >= copy_height * src_stride {
                        for row in 0..copy_height {
                            let src = row * src_stride;
                            let dst = row * dst_stride;
                            canvas[dst..dst + copy_width * 4]
                                .copy_from_slice(&frame.pixels[src..src + copy_width * 4]);
                        }
                    }
                }
                None => log::warn!("wayland: draw id={id} NO FRAME — rendering transparent pixels"),
            }
        }

        let layer = &self.surfaces[&id].layer;
        layer.wl_surface().damage_buffer(0, 0, width, height);

        if buffer.attach_to(layer.wl_surface()).is_ok() {
            layer.commit();
            log::debug!("wayland: draw id={id} committed {width}x{height}");
        } else {
            log::error!("wayland: draw id={id} buffer attach failed");
        }
    }

    fn hide(&self, id: i32) {
        if let Some(surface) = self.surfaces.get(&id) {
            surface.layer.wl_surface().attach(None, 0, 0);
            surface.layer.commit();
        }
    }

    fn sync(&mut self) {
        let game = {
            let game = lock(&self.shared.game);
            *game
        };

        let tracked: Vec<i32> = self
            .surfaces
            .iter()
            .filter(|(_, surface)| surface.tracked)
            .map(|(id, _)| *id)
            .collect();

        for id in tracked {
            if let Some(surface) = self.surfaces.get_mut(&id) {
                surface.apply_game(game);
            }
        }

        let visible: Vec<i32> = self
            .surfaces
            .iter()
            .filter(|(_, surface)| surface.visible)
            .map(|(id, _)| *id)
            .collect();

        for id in visible {
            self.draw(id);
        }
    }

    fn set_regions(&self, id: i32, rects: Vec<[i32; 4]>) {
        let Some(surface) = self.surfaces.get(&id) else {
            return;
        };

        let Ok(region) = Region::new(&self.compositor) else {
            return;
        };

        for [x, y, w, h] in rects {
            if w > 0 && h > 0 {
                region.add(x, y, w, h);
            }
        }

        surface
            .layer
            .wl_surface()
            .set_input_region(Some(region.wl_region()));
        surface.layer.commit();
    }

    fn set_keyboard(&mut self, id: i32, mode: i32) {
        let Some(surface) = self.surfaces.get_mut(&id) else {
            return;
        };

        let interactivity = match mode {
            1 => KeyboardInteractivity::Exclusive,
            2 => KeyboardInteractivity::OnDemand,
            _ => KeyboardInteractivity::None,
        };

        surface.layer.set_keyboard_interactivity(interactivity);
        surface.layer.commit();
    }

    fn handle(&mut self, command: Command) {
        match command {
            Command::Create { id, tracked } => {
                log::info!("wayland: create layer surface id={id} tracked={tracked}");
                let surface = self.compositor.create_surface(&self.qh);

                let layer = self.layer_shell.create_layer_surface(
                    &self.qh,
                    surface,
                    Layer::Overlay,
                    Some("froststrap-overlay"),
                    None,
                );

                layer.set_anchor(Anchor::TOP | Anchor::LEFT);
                layer.set_exclusive_zone(-1);
                layer.set_keyboard_interactivity(KeyboardInteractivity::None);
                layer.set_size(1, 1);

                if let Ok(region) = Region::new(&self.compositor) {
                    layer
                        .wl_surface()
                        .set_input_region(Some(region.wl_region()));
                }

                layer.commit();

                self.surfaces.insert(
                    id,
                    Surface {
                        layer,
                        tracked,
                        visible: true,
                        configured: false,
                        x: 0,
                        y: 0,
                        width: 1,
                        height: 1,
                    },
                );

                self.sync();
            }
            Command::Destroy(id) => {
                log::info!("wayland: destroy layer surface id={id}");
                self.surfaces.remove(&id);
                lock(&self.shared.frames).remove(&id);
            }
            Command::SetVisible { id, visible } => {
                log::info!("wayland: set_visible id={id} visible={visible}");
                if let Some(surface) = self.surfaces.get_mut(&id) {
                    surface.visible = visible;
                }

                if visible {
                    self.sync();
                } else {
                    self.hide(id);
                }
            }
            Command::SetRect {
                id,
                x,
                y,
                width,
                height,
            } => {
                let tracked = self
                    .surfaces
                    .get(&id)
                    .map(|surface| surface.tracked)
                    .unwrap_or(true);

                if !tracked {
                    if let Some(surface) = self.surfaces.get_mut(&id) {
                        surface.set_geometry(x, y, width, height);
                    }
                }

                if self.surfaces.get(&id).map(|s| s.visible).unwrap_or(false) {
                    self.draw(id);
                }
            }
            Command::SetKeyboard { id, mode } => self.set_keyboard(id, mode),
            Command::SetRegions { id, rects } => self.set_regions(id, rects),
            Command::Present(id) => self.draw(id),
            Command::Sync => self.sync(),
            Command::Shutdown => self.signal.stop(),
        }
    }
}

fn run(shared: Arc<Shared>, receiver: channel::Channel<Command>) -> Result<(), Box<dyn Error>> {
    let conn = Connection::connect_to_env()?;
    let (globals, event_queue) = registry_queue_init(&conn)?;
    let qh = event_queue.handle();

    let compositor = CompositorState::bind(&globals, &qh)?;
    let layer_shell = LayerShell::bind(&globals, &qh)?;
    let shm = Shm::bind(&globals, &qh)?;
    let pool = SlotPool::new(4096, &shm)?;

    let mut event_loop: EventLoop<App> = EventLoop::try_new()?;
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

    let mut app = App {
        registry_state: RegistryState::new(&globals),
        output_state: OutputState::new(&globals, &qh),
        compositor,
        seat_state: SeatState::new(&globals, &qh),
        layer_shell,
        shm,
        pool,
        shared,
        signal: event_loop.get_signal(),
        qh,
        surfaces: HashMap::new(),
        pointer: None,
        keyboard: None,
        keyboard_surface: None,
        modifiers: 0,
    };

    event_loop.run(None, &mut app, |_| {})?;
    Ok(())
}
