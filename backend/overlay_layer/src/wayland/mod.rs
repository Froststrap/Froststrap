mod handlers;
mod tracker;

use std::collections::VecDeque;
use std::error::Error;
use std::sync::atomic::{AtomicI32, Ordering};
use std::sync::{Arc, Mutex, MutexGuard};
use std::thread::{self, JoinHandle};

use smithay_client_toolkit::{
    compositor::{CompositorState, Region},
    output::OutputState,
    reexports::{
        calloop::{EventLoop, LoopSignal, channel},
        calloop_wayland_source::WaylandSource,
        client::{Connection, globals::registry_queue_init, protocol::wl_shm},
    },
    registry::RegistryState,
    seat::{SeatState, pointer::ThemedPointer},
    shell::{
        WaylandSurface,
        wlr_layer::{Anchor, KeyboardInteractivity, Layer, LayerShell, LayerSurface},
    },
    shm::{Shm, slot::SlotPool},
};

use tracker::{SoberRect, WindowTracker};

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
#[derive(Clone, Copy, Default)]
pub struct InputEvent {
    pub kind: i32,
    pub code: u32,
    pub x: f32,
    pub y: f32,
    pub dx: f32,
    pub dy: f32,
}

struct Shared {
    frame: Mutex<Frame>,
    width: AtomicI32,
    height: AtomicI32,
    input: Mutex<VecDeque<InputEvent>>,
}

enum Command {
    Present,
    Sync,
    SetVisible(bool),
    SetRegions(Vec<[i32; 4]>),
    Shutdown,
}

pub struct Overlay {
    shared: Arc<Shared>,
    sender: channel::Sender<Command>,
    thread: Option<JoinHandle<()>>,
}

impl Overlay {
    pub fn start() -> Overlay {
        let shared = Arc::new(Shared {
            frame: Mutex::new(Frame {
                width: 0,
                height: 0,
                pixels: Vec::new(),
            }),
            width: AtomicI32::new(0),
            height: AtomicI32::new(0),
            input: Mutex::new(VecDeque::new()),
        });
        let (sender, receiver) = channel::channel::<Command>();

        let thread_shared = shared.clone();
        let thread_sender = sender.clone();
        let thread = thread::spawn(move || {
            if let Err(err) = run(thread_shared, thread_sender, receiver) {
                eprintln!("overlay stopped: {err}");
            }
        });

        Overlay {
            shared,
            sender,
            thread: Some(thread),
        }
    }

    pub fn size(&self) -> (i32, i32) {
        (
            self.shared.width.load(Ordering::Relaxed),
            self.shared.height.load(Ordering::Relaxed),
        )
    }

    pub fn submit(&self, pixels: &[u8], width: i32, height: i32, stride: i32) {
        if width <= 0 || height <= 0 || stride < width * 4 {
            return;
        }

        let row_bytes = width as usize * 4;
        let needed = stride as usize * (height as usize - 1) + row_bytes;
        if pixels.len() < needed {
            return;
        }

        {
            let mut frame = lock(&self.shared.frame);
            frame.width = width;
            frame.height = height;
            frame.pixels.resize(row_bytes * height as usize, 0);
            for row in 0..height as usize {
                let src = row * stride as usize;
                let dst = row * row_bytes;
                frame.pixels[dst..dst + row_bytes].copy_from_slice(&pixels[src..src + row_bytes]);
            }
        }

        let _ = self.sender.send(Command::Present);
    }

    pub fn set_visible(&self, visible: bool) {
        let _ = self.sender.send(Command::SetVisible(visible));
    }

    pub fn set_regions(&self, rects: Vec<[i32; 4]>) {
        let _ = self.sender.send(Command::SetRegions(rects));
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
        let _ = self.sender.send(Command::Shutdown);
        if let Some(thread) = self.thread.take() {
            let _ = thread.join();
        }
    }
}

struct App {
    registry_state: RegistryState,
    output_state: OutputState,
    shm: Shm,
    pool: SlotPool,
    layer: LayerSurface,
    tracker: Box<dyn WindowTracker>,
    shared: Arc<Shared>,
    signal: LoopSignal,
    rect: Option<SoberRect>,
    visible: bool,
    configured: bool,
    compositor: CompositorState,
    seat_state: SeatState,
    pointer: Option<ThemedPointer>,
}

impl App {
    fn draw(&mut self) {
        let Some(rect) = self.rect else {
            return;
        };
        if !self.configured {
            return;
        }

        let width = rect.width.max(1);
        let height = rect.height.max(1);
        let Ok((buffer, canvas)) =
            self.pool
                .create_buffer(width, height, width * 4, wl_shm::Format::Argb8888)
        else {
            return;
        };

        canvas.fill(0);

        {
            let frame = lock(&self.shared.frame);
            let copy_width = frame.width.min(width).max(0) as usize;
            let copy_height = frame.height.min(height).max(0) as usize;
            let src_stride = frame.width as usize * 4;
            let dst_stride = width as usize * 4;
            for row in 0..copy_height {
                let src = row * src_stride;
                let dst = row * dst_stride;
                canvas[dst..dst + copy_width * 4]
                    .copy_from_slice(&frame.pixels[src..src + copy_width * 4]);
            }
        }

        self.layer.wl_surface().damage_buffer(0, 0, width, height);
        if buffer.attach_to(self.layer.wl_surface()).is_ok() {
            self.layer.commit();
        }
    }

    fn sync(&mut self) {
        let next = if self.visible {
            self.tracker.query()
        } else {
            None
        };
        if next == self.rect {
            return;
        }
        self.rect = next;

        match next {
            Some(rect) => {
                self.shared.width.store(rect.width, Ordering::Relaxed);
                self.shared.height.store(rect.height, Ordering::Relaxed);
                self.layer
                    .set_size(rect.width.max(1) as u32, rect.height.max(1) as u32);
                self.layer.set_margin(rect.y, 0, 0, rect.x);
                self.layer.commit();
            }
            None => {
                self.shared.width.store(0, Ordering::Relaxed);
                self.shared.height.store(0, Ordering::Relaxed);
                self.configured = false;
                self.layer.wl_surface().attach(None, 0, 0);
                self.layer.commit();
            }
        }
    }

    fn push_input(&self, event: InputEvent) {
        let mut queue = lock(&self.shared.input);
        if queue.len() >= 1024 {
            queue.pop_front();
        }
        queue.push_back(event);
    }

    fn set_regions(&mut self, rects: Vec<[i32; 4]>) {
        let Ok(region) = Region::new(&self.compositor) else {
            return;
        };
        for [x, y, w, h] in rects {
            region.add(x, y, w, h);
        }
        self.layer
            .wl_surface()
            .set_input_region(Some(region.wl_region()));
        self.layer.commit();
    }

    fn handle(&mut self, command: Command) {
        match command {
            Command::Present => self.draw(),
            Command::Sync => self.sync(),
            Command::SetVisible(visible) => {
                self.visible = visible;
                self.sync();
            }
            Command::SetRegions(rects) => self.set_regions(rects),
            Command::Shutdown => self.signal.stop(),
        }
    }
}

fn run(
    shared: Arc<Shared>,
    sender: channel::Sender<Command>,
    receiver: channel::Channel<Command>,
) -> Result<(), Box<dyn Error>> {
    let tracker = tracker::detect().ok_or("no supported compositor detected")?;

    let conn = Connection::connect_to_env()?;
    let (globals, event_queue) = registry_queue_init(&conn)?;
    let qh = event_queue.handle();

    let compositor = CompositorState::bind(&globals, &qh)?;
    let layer_shell = LayerShell::bind(&globals, &qh)?;
    let shm = Shm::bind(&globals, &qh)?;

    let surface = compositor.create_surface(&qh);
    let layer = layer_shell.create_layer_surface(
        &qh,
        surface,
        Layer::Overlay,
        Some("froststrap-overlay"),
        None,
    );
    layer.set_anchor(Anchor::TOP | Anchor::LEFT);
    layer.set_exclusive_zone(-1);
    layer.set_keyboard_interactivity(KeyboardInteractivity::None);

    let empty_region = Region::new(&compositor)?;
    layer
        .wl_surface()
        .set_input_region(Some(empty_region.wl_region()));

    let initial = tracker.query();
    match initial {
        Some(rect) => {
            shared.width.store(rect.width, Ordering::Relaxed);
            shared.height.store(rect.height, Ordering::Relaxed);
            layer.set_size(rect.width.max(1) as u32, rect.height.max(1) as u32);
            layer.set_margin(rect.y, 0, 0, rect.x);
        }
        None => layer.set_size(1, 1),
    }
    layer.commit();

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

    if let Some(events) = tracker.watch() {
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
        shm,
        pool,
        layer,
        tracker,
        shared,
        signal: event_loop.get_signal(),
        rect: initial,
        visible: true,
        configured: false,
        compositor,
        seat_state: SeatState::new(&globals, &qh),
        pointer: None,
    };

    event_loop.run(None, &mut app, |_| {})?;
    Ok(())
}
