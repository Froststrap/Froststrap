
use std::sync::mpsc::{self, Receiver};
use std::sync::{Arc, Mutex};
use std::thread;
use std::time::Duration;

use zbus::blocking::ConnectionBuilder;
use zbus::blocking::Connection;

use super::{SoberRect, WindowTracker, output_hash};

const KWIN_SERVICE: &str = "org.kde.KWin";
const SCRIPTING_PATH: &str = "/Scripting";
const SCRIPTING_IFACE: &str = "org.kde.KWin.Scripting";

const CALLBACK_SERVICE: &str = "org.froststrap.Overlay";
const CALLBACK_PATH: &str = "/froststrap";

const SOBER_CLASS: &str = "org.vinegarhq.Sober";

const SCRIPT_NAME: &str = "froststrap-overlay";

#[derive(Default)]
struct State {
    seen: bool,
    class: String,
    pid: i32,
    #[allow(dead_code)]
    uuid: String,
    rect: Option<SoberRect>,
}

#[derive(Clone)]
struct Callback {
    state: Arc<Mutex<State>>,
}

impl Callback {
    fn record(&self, class: String, pid: String, uuid: String, screen: String, x: String, y: String, w: String, h: String) {
        let pid = pid.trim().parse::<i32>().unwrap_or(0);
        let monitor = output_hash(&screen);
        let rect = match (num(&x), num(&y), num(&w), num(&h)) {
            (Some(x), Some(y), Some(w), Some(h)) if w > 0.0 && h > 0.0 => Some(SoberRect {
                monitor,
                x: x.round() as i32,
                y: y.round() as i32,
                width: w.round() as i32,
                height: h.round() as i32,
            }),
            _ => None,
        };

        if let Ok(mut state) = self.state.lock() {
            state.seen = true;
            state.class = class;
            state.pid = pid;
            state.uuid = uuid;
            state.rect = rect;
        }
    }
}

fn num(value: &str) -> Option<f64> {
    value.trim().parse::<f64>().ok()
}

#[zbus::interface(name = "org.froststrap.Overlay")]
impl Callback {
    fn active_window(&self, class: String, pid: String, uuid: String, screen: String, x: String, y: String, width: String, height: String) {
        self.record(class, pid, uuid, screen, x, y, width, height);
    }
}

pub struct Kde {
    state: Arc<Mutex<State>>,
    process_id: i32,
    _connection: Connection,
}

impl Kde {
    pub fn try_new(process_id: i32) -> Option<Self> {
        let probe = Connection::session().ok()?;
        probe
            .call_method(
                Some("org.freedesktop.DBus"),
                "/org/freedesktop/DBus",
                Some("org.freedesktop.DBus"),
                "GetNameOwner",
                &(KWIN_SERVICE,),
            )
            .ok()?;
        drop(probe);

        let state = Arc::new(Mutex::new(State::default()));

        let connection = ConnectionBuilder::session()
            .ok()?
            .name(CALLBACK_SERVICE)
            .ok()?
            .serve_at(CALLBACK_PATH, Callback { state: state.clone() })
            .ok()?
            .build()
            .ok()?;

        let path = match script_path() {
            Some(path) => path,
            None => return None,
        };
        if std::fs::write(&path, SCRIPT).is_err() {
            return None;
        }

        let script = path.to_string_lossy().to_string();

        let _ = connection.call_method(
            Some(KWIN_SERVICE),
            SCRIPTING_PATH,
            Some(SCRIPTING_IFACE),
            "unloadScript",
            &(SCRIPT_NAME,),
        );

        let id = match connection.call_method(
            Some(KWIN_SERVICE),
            SCRIPTING_PATH,
            Some(SCRIPTING_IFACE),
            "loadScript",
            &(script.as_str(),),
        ) {
            Ok(reply) => reply.body().deserialize::<i32>().unwrap_or(-1),
            Err(_) => return None,
        };

        let object = format!("/Scripting/Script{id}");
        if id <= 0
            || connection
                .call_method(
                    Some(KWIN_SERVICE),
                    object.as_str(),
                    Some("org.kde.KWin.Script"),
                    "run",
                    &(),
                )
                .is_err()
        {
            let _ = connection.call_method(
                Some(KWIN_SERVICE),
                SCRIPTING_PATH,
                Some(SCRIPTING_IFACE),
                "start",
                &(),
            );
        }

        eprintln!("kde: using a KWin script for active-window tracking");

        Some(Kde { state, process_id, _connection: connection })
    }

    fn with_state<T>(&self, f: impl FnOnce(&State) -> T) -> Option<T> {
        self.state.lock().ok().map(|state| f(&state))
    }
}

impl Drop for Kde {
    fn drop(&mut self) {
        let _ = self._connection.call_method(
            Some(KWIN_SERVICE),
            SCRIPTING_PATH,
            Some(SCRIPTING_IFACE),
            "unloadScript",
            &(SCRIPT_NAME,),
        );
    }
}

impl WindowTracker for Kde {
    fn query(&self) -> Option<SoberRect> {
        self.with_state(|state| {
            if state.class == SOBER_CLASS { state.rect } else { None }
        })
        .flatten()
    }

    fn watch(&self) -> Option<Receiver<()>> {
        let (tx, rx) = mpsc::channel();
        thread::spawn(move || {
            while tx.send(()).is_ok() {
                thread::sleep(Duration::from_millis(150));
            }
        });
        Some(rx)
    }

    fn uses_output(&self) -> bool { true }

    fn focus(&self) {}

    fn foreground(&self) -> i32 {
        self.with_state(|state| {
            if state.class == SOBER_CLASS { self.process_id } else { 0 }
        })
        .unwrap_or(0)
    }

    fn focus_known(&self) -> bool {
        self.with_state(|state| state.seen).unwrap_or(false)
    }

    fn minimised(&self) -> bool { false }
}

fn script_path() -> Option<std::path::PathBuf> {
    let dir = std::env::var_os("XDG_RUNTIME_DIR")
        .map(std::path::PathBuf::from)
        .unwrap_or_else(std::env::temp_dir);
    Some(dir.join(format!("{SCRIPT_NAME}.js")))
}

const SCRIPT: &str = r#"
var FROSTSTRAP_SOBER = "org.vinegarhq.Sober";

function froststrapActive() {
    try {
        if (workspace.activeWindow !== undefined) {
            return workspace.activeWindow;
        }
        if (workspace.activeClient !== undefined) {
            return workspace.activeClient;
        }
    } catch (e) {
    }
    return null;
}

function froststrapClass(w) {
    try {
        if (w.resourceClass) {
            return String(w.resourceClass);
        }
    } catch (e) {
    }
    return "";
}

function froststrapPush() {
    var w = froststrapActive();
    var cls = "";
    var pid = "0";
    var uuid = "";
    var screen = "";
    var gx = "";
    var gy = "";
    var gw = "";
    var gh = "";
    if (w) {
        cls = froststrapClass(w);
        try {
            if (w.pid !== undefined) {
                pid = String(w.pid);
            }
        } catch (e) {
        }
        try {
            if (w.internalId) {
                uuid = String(w.internalId);
            }
        } catch (e) {
        }
        try {
            if (w.output && w.output.name) {
                screen = String(w.output.name);
            } else if (w.screen !== undefined) {
                screen = String(w.screen);
            }
        } catch (e) {
        }
        try {
            var g = w.frameGeometry;
            if (g) {
                gx = String(g.x);
                gy = String(g.y);
                gw = String(g.width);
                gh = String(g.height);
            }
        } catch (e) {
        }
    }
    callDBus("org.froststrap.Overlay", "/froststrap", "org.froststrap.Overlay",
             "ActiveWindow", cls, pid, uuid, screen, gx, gy, gw, gh);
}

try {
    workspace.windowActivated.connect(froststrapPush);
} catch (e) {
}
try {
    workspace.clientActivated.connect(froststrapPush);
} catch (e) {
}
try {
    workspace.windowGeometryChanged.connect(froststrapPush);
} catch (e) {
}
try {
    workspace.clientGeometryChanged.connect(froststrapPush);
} catch (e) {
}
try {
    workspace.windowRemoved.connect(froststrapPush);
} catch (e) {
}

froststrapPush();
"#;
