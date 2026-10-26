use std::collections::HashMap;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::mpsc::{self, Receiver};
use std::sync::{Arc, Mutex};
use std::thread;
use std::time::Duration;

use wayland_client::{
    backend::ObjectData,
    globals::{registry_queue_init, GlobalListContents},
    protocol::{wl_output, wl_registry, wl_seat},
    Connection, Dispatch, Proxy, QueueHandle,
};
use wayland_protocols_wlr::foreign_toplevel::v1::client::{
    zwlr_foreign_toplevel_handle_v1::{self, ZwlrForeignToplevelHandleV1},
    zwlr_foreign_toplevel_manager_v1::{self, ZwlrForeignToplevelManagerV1},
};

use super::{SoberRect, WindowTracker, output_hash};

const SOBER_APP_ID: &str = "org.vinegarhq.Sober";
const STATE_MINIMIZED: u32 = 1;
const STATE_ACTIVATED: u32 = 2;

struct Toplevel {
    handle: ZwlrForeignToplevelHandleV1,
    app_id: String,
    activated: bool,
    minimized: bool,
    closed: bool,
    outputs: Vec<u32>,
}

#[derive(Default)]
struct State {
    toplevels: HashMap<u32, Toplevel>,
    outputs: HashMap<u32, String>,
}

#[derive(Default)]
struct Snapshot {
    found: bool,
    activated: bool,
    minimized: bool,
    handle: Option<ZwlrForeignToplevelHandleV1>,
    output: Option<i64>,
}

fn has_state(raw: &[u8], wanted: u32) -> bool {
    raw.chunks_exact(4)
        .any(|chunk| u32::from_ne_bytes([chunk[0], chunk[1], chunk[2], chunk[3]]) == wanted)
}

impl State {
    fn snapshot(&self) -> Snapshot {
        match self
            .toplevels
            .values()
            .find(|t| t.app_id == SOBER_APP_ID && !t.closed)
        {
            Some(toplevel) => Snapshot {
                found: true,
                activated: toplevel.activated,
                minimized: toplevel.minimized,
                handle: Some(toplevel.handle.clone()),
                output: toplevel
                    .outputs
                    .iter()
                    .find_map(|id| self.outputs.get(id))
                    .map(|name| output_hash(name)),
            },
            None => Snapshot::default(),
        }
    }

    fn toplevel(&mut self, id: u32, handle: &ZwlrForeignToplevelHandleV1) -> &mut Toplevel {
        self.toplevels.entry(id).or_insert_with(|| Toplevel {
            handle: handle.clone(),
            app_id: String::new(),
            activated: false,
            minimized: false,
            closed: false,
            outputs: Vec::new(),
        })
    }
}

impl Dispatch<ZwlrForeignToplevelManagerV1, ()> for State {
    fn event(
        _state: &mut Self,
        _proxy: &ZwlrForeignToplevelManagerV1,
        _event: zwlr_foreign_toplevel_manager_v1::Event,
        _data: &(),
        _conn: &Connection,
        _qh: &QueueHandle<Self>,
    ) {
    }

    fn event_created_child(
        _opcode: u16,
        qhandle: &QueueHandle<Self>,
    ) -> Arc<dyn ObjectData> {
        qhandle.make_data::<ZwlrForeignToplevelHandleV1, ()>(())
    }
}

impl Dispatch<ZwlrForeignToplevelHandleV1, ()> for State {
    fn event(
        state: &mut Self,
        proxy: &ZwlrForeignToplevelHandleV1,
        event: zwlr_foreign_toplevel_handle_v1::Event,
        _data: &(),
        _conn: &Connection,
        _qh: &QueueHandle<Self>,
    ) {
        let handle_id = proxy.id().protocol_id();
        match event {
            zwlr_foreign_toplevel_handle_v1::Event::AppId { app_id } => {
                state.toplevel(handle_id, proxy).app_id = app_id;
            }
            zwlr_foreign_toplevel_handle_v1::Event::State { state: raw } => {
                let activated = has_state(&raw, STATE_ACTIVATED);
                let minimized = has_state(&raw, STATE_MINIMIZED);
                let toplevel = state.toplevel(handle_id, proxy);
                toplevel.activated = activated;
                toplevel.minimized = minimized;
            }
            zwlr_foreign_toplevel_handle_v1::Event::OutputEnter { output } => {
                let output_id = output.id().protocol_id();
                let toplevel = state.toplevel(handle_id, proxy);
                if !toplevel.outputs.contains(&output_id) {
                    toplevel.outputs.push(output_id);
                }
            }
            zwlr_foreign_toplevel_handle_v1::Event::OutputLeave { output } => {
                let output_id = output.id().protocol_id();
                state
                    .toplevel(handle_id, proxy)
                    .outputs
                    .retain(|id| *id != output_id);
            }
            zwlr_foreign_toplevel_handle_v1::Event::Closed => {
                state.toplevel(handle_id, proxy).closed = true;
            }
            _ => {}
        }
    }
}

impl Dispatch<wl_output::WlOutput, ()> for State {
    fn event(
        state: &mut Self,
        proxy: &wl_output::WlOutput,
        event: wl_output::Event,
        _data: &(),
        _conn: &Connection,
        _qh: &QueueHandle<Self>,
    ) {
        if let wl_output::Event::Name { name } = event {
            state.outputs.insert(proxy.id().protocol_id(), name);
        }
    }
}

impl Dispatch<wl_registry::WlRegistry, GlobalListContents> for State {
    fn event(
        _state: &mut Self,
        _proxy: &wl_registry::WlRegistry,
        _event: wl_registry::Event,
        _data: &GlobalListContents,
        _conn: &Connection,
        _qh: &QueueHandle<Self>,
    ) {
    }
}

impl Dispatch<wl_seat::WlSeat, ()> for State {
    fn event(
        _state: &mut Self,
        _proxy: &wl_seat::WlSeat,
        _event: wl_seat::Event,
        _data: &(),
        _conn: &Connection,
        _qh: &QueueHandle<Self>,
    ) {
    }
}

pub struct ForeignToplevel {
    snapshot: Arc<Mutex<Snapshot>>,
    process_id: i32,
    dirty: Arc<AtomicBool>,
    manager: ZwlrForeignToplevelManagerV1,
    seat: Option<wl_seat::WlSeat>,
}

impl ForeignToplevel {
    pub fn try_new(process_id: i32) -> Option<Self> {
        let conn = Connection::connect_to_env().ok()?;
        let (globals, mut queue) = registry_queue_init::<State>(&conn).ok()?;
        let qh = queue.handle();

        let manager: ZwlrForeignToplevelManagerV1 = globals.bind(&qh, 3..=3, ()).ok()?;
        let seat: Option<wl_seat::WlSeat> = globals.bind(&qh, 1..=9, ()).ok();

        let mut state = State::default();
        for global in globals.contents().clone_list() {
            if global.interface == "wl_output" {
                let version = global.version.min(4);
                let _output: wl_output::WlOutput = globals.registry().bind(global.name, version, &qh, ());
            }
        }
        queue.roundtrip(&mut state).ok()?;

        let snapshot = Arc::new(Mutex::new(state.snapshot()));
        let dirty = Arc::new(AtomicBool::new(false));
        let thread_snapshot = snapshot.clone();
        let thread_dirty = dirty.clone();

        thread::Builder::new()
            .name("foreign-toplevel".into())
            .spawn(move || loop {
                if queue.blocking_dispatch(&mut state).is_err() {
                    return;
                }
                if let Ok(mut guard) = thread_snapshot.lock() {
                    *guard = state.snapshot();
                }
                thread_dirty.store(true, Ordering::SeqCst);
            })
            .ok()?;

        Some(ForeignToplevel { snapshot, process_id, dirty, manager, seat })
    }

    fn sober(&self) -> Option<(bool, bool, Option<ZwlrForeignToplevelHandleV1>)> {
        let guard = self.snapshot.lock().ok()?;
        if !guard.found {
            return None;
        }
        Some((guard.activated, guard.minimized, guard.handle.clone()))
    }
}

impl WindowTracker for ForeignToplevel {
    fn query(&self) -> Option<SoberRect> {
        None
    }

    fn watch(&self) -> Option<Receiver<()>> {
        let (tx, rx) = mpsc::channel();
        let dirty = self.dirty.clone();
        thread::spawn(move || loop {
            thread::sleep(Duration::from_millis(120));
            if dirty.swap(false, Ordering::SeqCst) && tx.send(()).is_err() {
                break;
            }
        });
        Some(rx)
    }

    fn uses_output(&self) -> bool {
        true
    }

    fn focus(&self) {
        if let Some((_, _, Some(handle))) = self.sober()
            && let Some(seat) = self.seat.as_ref()
        {
            handle.activate(seat);
        }
    }

    fn foreground(&self) -> i32 {
        match self.sober() {
            Some((true, _, _)) => self.process_id,
            _ => 0,
        }
    }

    fn minimised(&self) -> bool {
        matches!(self.sober(), Some((_, true, _)))
    }

    fn game_output(&self) -> Option<i64> {
        self.snapshot.lock().ok()?.output
    }
}

impl Drop for ForeignToplevel {
    fn drop(&mut self) {
        self.manager.stop();
    }
}