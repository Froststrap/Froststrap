use std::collections::HashMap;
use std::sync::atomic::{AtomicBool, AtomicI32, Ordering};
use std::sync::mpsc::{self, Receiver};
use std::sync::Arc;
use std::thread;
use std::time::Duration;

use zbus::blocking::Connection;
use zbus::zvariant::OwnedValue;

use super::{SoberRect, WindowTracker};

const SHELL_SERVICE: &str = "org.gnome.Shell";
const EXT_PATH: &str = "/org/gnome/Shell/Extensions/WindowsExt";
const EXT_IFACE: &str = "org.gnome.Shell.Extensions.WindowsExt";
const INTROSPECT_IFACE: &str = "org.gnome.Shell.Introspect";
const INTROSPECT_PATH: &str = "/org/gnome/Shell/Introspect";

const POLL_INTERVAL: Duration = Duration::from_millis(100);

enum Backend {
    Extension,
    Introspect,
}

pub struct Gnome {
    process_id: i32,
    focused: Arc<AtomicI32>,
    known: Arc<AtomicBool>,
    alive: Arc<AtomicBool>,
}

fn focused_pid(conn: &Connection, backend: &Backend) -> Option<i32> {
    match backend {
        Backend::Extension => {
            let reply = conn
                .call_method(Some(SHELL_SERVICE), EXT_PATH, Some(EXT_IFACE), "FocusPID", &())
                .ok()?;
            let body = reply.body();
            if let Ok(pid) = body.deserialize::<u32>() {
                return Some(pid as i32);
            }
            let text = body.deserialize::<String>().ok()?;
            text.parse().ok()
        }
        Backend::Introspect => {
            let reply = conn
                .call_method(Some(SHELL_SERVICE), INTROSPECT_PATH, Some(INTROSPECT_IFACE), "GetWindows", &())
                .ok()?;
            let windows = reply
                .body()
                .deserialize::<HashMap<u64, HashMap<String, OwnedValue>>>()
                .ok()?;
            for props in windows.values() {
                let focused = props
                    .get("has-focus")
                    .and_then(|v| bool::try_from(v).ok())
                    .unwrap_or(false);
                if !focused {
                    continue;
                }
                return Some(
                    props
                        .get("pid")
                        .and_then(|v| u32::try_from(v).ok())
                        .unwrap_or(0) as i32,
                );
            }
            Some(0)
        }
    }
}

impl Gnome {
    pub fn try_new(process_id: i32) -> Option<Self> {
        let conn = Connection::session().ok()?;

        let backend = if conn
            .call_method(Some(SHELL_SERVICE), EXT_PATH, Some(EXT_IFACE), "FocusPID", &())
            .is_ok()
        {
            eprintln!("gnome: using Window Calls Extended for focus tracking");
            Backend::Extension
        } else if conn
            .call_method(Some(SHELL_SERVICE), INTROSPECT_PATH, Some(INTROSPECT_IFACE), "GetWindows", &())
            .is_ok()
        {
            eprintln!("gnome: using org.gnome.Shell.Introspect for focus tracking");
            Backend::Introspect
        } else {
            eprintln!(
                "gnome: no window API available. Install the Window Calls Extended \
                 shell extension for focus detection; bounds and activation are not \
                 exposed by Mutter to third-party clients"
            );
            return None;
        };

        drop(conn);

        let focused = Arc::new(AtomicI32::new(0));
        let known = Arc::new(AtomicBool::new(false));
        let alive = Arc::new(AtomicBool::new(true));

        let thread_focused = focused.clone();
        let thread_known = known.clone();
        let thread_alive = alive.clone();

        thread::Builder::new()
            .name("gnome-focus".into())
            .spawn(move || {
                let Ok(conn) = Connection::session() else { return };
                while thread_alive.load(Ordering::SeqCst) {
                    if let Some(pid) = focused_pid(&conn, &backend) {
                        thread_focused.store(pid, Ordering::SeqCst);
                        thread_known.store(true, Ordering::SeqCst);
                    }
                    thread::sleep(POLL_INTERVAL);
                }
            })
            .ok()?;

        Some(Gnome { process_id, focused, known, alive })
    }

    fn focused(&self) -> i32 {
        self.focused.load(Ordering::SeqCst)
    }
}

impl WindowTracker for Gnome {
    fn query(&self) -> Option<SoberRect> {
        None
    }

    fn watch(&self) -> Option<Receiver<()>> {
        let (tx, rx) = mpsc::channel();
        thread::spawn(move || {
            while tx.send(()).is_ok() {
                thread::sleep(Duration::from_millis(250));
            }
        });
        Some(rx)
    }

    fn uses_output(&self) -> bool {
        true
    }

    fn focus(&self) {
        eprintln!("gnome: FocusGame is not supported by Mutter without a custom shell extension");
    }

    fn focus_known(&self) -> bool {
        self.known.load(Ordering::SeqCst)
    }

    fn foreground(&self) -> i32 {
        if self.known.load(Ordering::SeqCst) && self.focused() == self.process_id {
            self.process_id
        } else {
            0
        }
    }

    fn minimised(&self) -> bool {
        false
    }
}

impl Drop for Gnome {
    fn drop(&mut self) {
        self.alive.store(false, Ordering::SeqCst);
    }
}
