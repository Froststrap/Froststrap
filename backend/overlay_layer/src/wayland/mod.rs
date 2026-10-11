mod handlers;
mod tracker;

use std::thread;

use smithay_client_toolkit::{
    compositor::{CompositorState, Region},
    output::OutputState,
    reexports::{
        calloop::{channel, EventLoop},
        calloop_wayland_source::WaylandSource,
        client::{globals::registry_queue_init, protocol::wl_shm, Connection},
    },
    registry::RegistryState,
    shell::{
        wlr_layer::{Anchor, KeyboardInteractivity, Layer, LayerShell, LayerSurface},
        WaylandSurface,
    },
    shm::{slot::SlotPool, Shm},
};

use tracker::{SoberRect, WindowTracker};

const BORDER: i32 = 6;
const BORDER_PIXEL: u32 = 0xFF2080FF;
const FILL_PIXEL: u32 = 0x20001040;

pub struct App {
    registry_state: RegistryState,
    output_state: OutputState,
    shm: Shm,
    pool: SlotPool,
    layer: LayerSurface,
    tracker: Box<dyn WindowTracker>,
    rect: Option<SoberRect>,
}

impl App {
    fn draw(&mut self) {
        let Some(rect) = self.rect else {
            return;
        };

        let width = rect.width.max(1);
        let height = rect.height.max(1);
        let (buffer, canvas) = self
            .pool
            .create_buffer(width, height, width * 4, wl_shm::Format::Argb8888)
            .expect("create buffer");

        for (index, px) in canvas.chunks_exact_mut(4).enumerate() {
            let x = index as i32 % width;
            let y = index as i32 / width;
            let on_border =
                x < BORDER || y < BORDER || x >= width - BORDER || y >= height - BORDER;
            let pixel = if on_border { BORDER_PIXEL } else { FILL_PIXEL };
            px.copy_from_slice(&pixel.to_le_bytes());
        }

        self.layer.wl_surface().damage_buffer(0, 0, width, height);
        buffer
            .attach_to(self.layer.wl_surface())
            .expect("attach buffer");
        self.layer.commit();
    }

    fn sync(&mut self) {
        let next = self.tracker.query();
        if next == self.rect {
            return;
        }
        self.rect = next;

        match next {
            Some(rect) => {
                self.layer
                    .set_size(rect.width.max(1) as u32, rect.height.max(1) as u32);
                self.layer.set_margin(rect.y, 0, 0, rect.x);
                self.layer.commit();
            }
            None => {
                self.layer.wl_surface().attach(None, 0, 0);
                self.layer.commit();
            }
        }
    }
}

pub fn run_rect_demo() -> Result<(), Box<dyn std::error::Error>> {
    let tracker = tracker::detect().ok_or("no supported compositor detected")?;
    let initial = tracker.query();

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
    layer.wl_surface().set_input_region(Some(empty_region.wl_region()));

    match initial {
        Some(rect) => {
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

    let (tx, rx) = channel::channel::<()>();
    if let Some(events) = tracker.watch() {
        thread::spawn(move || {
            while events.recv().is_ok() {
                if tx.send(()).is_err() {
                    break;
                }
            }
        });
    }

    handle
        .insert_source(rx, |event, _, app| {
            if let channel::Event::Msg(()) = event {
                app.sync();
            }
        })
        .map_err(|_| "failed to register the tracker channel")?;

    let mut app = App {
        registry_state: RegistryState::new(&globals),
        output_state: OutputState::new(&globals, &qh),
        shm,
        pool,
        layer,
        tracker,
        rect: initial,
    };

    loop {
        event_loop.dispatch(None, &mut app)?;
    }
}
