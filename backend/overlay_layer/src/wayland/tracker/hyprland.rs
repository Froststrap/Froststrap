use super::{SoberRect, WindowTracker};
use std::collections::HashMap;
use std::io::{BufRead, BufReader, Read, Write};
use std::os::unix::net::UnixStream;
use std::path::PathBuf;
use std::sync::mpsc::{self, Receiver};
use std::thread;
use std::time::Duration;
use tinyjson::JsonValue;

const SOBER_CLASS: &str = "org.vinegarhq.Sober";

fn socket_path(name: &str) -> Option<PathBuf> {
    let runtime = std::env::var_os("XDG_RUNTIME_DIR")?;
    let signature = std::env::var_os("HYPRLAND_INSTANCE_SIGNATURE")?;
    Some(PathBuf::from(runtime).join("hypr").join(signature).join(name))
}

fn request(command: &str) -> Option<JsonValue> {
    let mut stream = UnixStream::connect(socket_path(".socket.sock")?).ok()?;
    stream.write_all(command.as_bytes()).ok()?;
    let mut reply = String::new();
    stream.read_to_string(&mut reply).ok()?;
    reply.parse::<JsonValue>().ok()
}

fn command(text: &str) -> bool {
    let Some(path) = socket_path(".socket.sock") else { return false };
    let Ok(mut stream) = UnixStream::connect(path) else { return false };
    stream.write_all(text.as_bytes()).is_ok()
}

fn field<'a>(value: &'a JsonValue, key: &str) -> Option<&'a JsonValue> {
    value.get::<HashMap<String, JsonValue>>()?.get(key)
}

fn array(value: &JsonValue) -> Option<&Vec<JsonValue>> { value.get::<Vec<JsonValue>>() }
fn int(value: &JsonValue) -> Option<i64> { value.get::<f64>().map(|n| *n as i64) }
fn flag(value: &JsonValue) -> Option<bool> { value.get::<bool>().copied() }
fn text(value: &JsonValue) -> Option<&str> { value.get::<String>().map(|s| s.as_str()) }

fn rect_for(client: &JsonValue, monitors: &[JsonValue]) -> Option<SoberRect> {
    if text(field(client, "class")?)? != SOBER_CLASS {
        return None;
    }
    if flag(field(client, "mapped")?)? != true
        || field(client, "hidden").and_then(flag) == Some(true)
    {
        return None;
    }

    let monitor_id = int(field(client, "monitor")?)?;
    let workspace_id = int(field(field(client, "workspace")?, "id")?)?;
    let monitor = monitors
        .iter()
        .find(|m| field(m, "id").and_then(int) == Some(monitor_id))?;

    if int(field(field(monitor, "activeWorkspace")?, "id")?)? != workspace_id {
        return None;
    }

    let at = array(field(client, "at")?)?;
    let size = array(field(client, "size")?)?;

    Some(SoberRect {
        monitor: monitor_id,
        x: int(at.first()?)? as i32,
        y: int(at.get(1)?)? as i32,
        width: int(size.first()?)? as i32,
        height: int(size.get(1)?)? as i32,
    })
}

fn frame_interval() -> Duration {
    let hz = request("j/monitors")
        .and_then(|monitors| {
            array(&monitors).map(|list| {
                list.iter()
                    .filter_map(|monitor| field(monitor, "refreshRate")?.get::<f64>().copied())
                    .fold(0.0, f64::max)
            })
        })
        .filter(|hz| *hz >= 1.0)
        .unwrap_or(60.0);
    Duration::from_secs_f64(1.0 / hz)
}

fn watch() -> Option<Receiver<()>> {
    let stream = UnixStream::connect(socket_path(".socket2.sock")?).ok()?;
    let (tx, rx) = mpsc::channel();

    let ticker = tx.clone();
    thread::spawn(move || {
        let mut interval = frame_interval();
        let mut ticks = 0u32;
        while ticker.send(()).is_ok() {
            thread::sleep(interval);
            ticks += 1;
            if ticks % 120 == 0 {
                interval = frame_interval();
            }
        }
    });

    thread::spawn(move || {
        for line in BufReader::new(stream).lines() {
            if line.is_err() || tx.send(()).is_err() {
                break;
            }
        }
    });

    Some(rx)
}

pub struct Hyprland {
    process_id: i32,
    address: std::sync::Mutex<Option<String>>,
}

impl Hyprland {
    pub fn new(process_id: i32) -> Hyprland {
        Hyprland { process_id, address: std::sync::Mutex::new(None) }
    }

    fn refresh_address(&self) {
        let Some(clients) = request("j/clients") else { return };
        let Some(list) = array(&clients) else { return };
        let found = list.iter().find_map(|client| {
            if text(field(client, "class")?)? != SOBER_CLASS {
                return None;
            }
            text(field(client, "address")?).map(|s| s.to_string())
        });
        if let Ok(mut guard) = self.address.lock() {
            *guard = found;
        }
    }
}

impl WindowTracker for Hyprland {
    fn query(&self) -> Option<SoberRect> {
        let clients = request("j/clients")?;
        let monitors = request("j/monitors")?;
        let monitors = array(&monitors)?;
        array(&clients)?.iter().find_map(|client| rect_for(client, monitors))
    }

    fn watch(&self) -> Option<Receiver<()>> {
        watch()
    }

    fn focus(&self) {
        self.refresh_address();
        if let Ok(guard) = self.address.lock()
            && let Some(address) = guard.as_ref()
        {
            let _ = command(&format!("dispatch focuswindow address:{address}"));
        }
    }

    fn foreground(&self) -> i32 {
        let Some(clients) = request("j/activewindow") else { return 0 };
        let class = field(&clients, "class").and_then(text).unwrap_or("");
        if class == SOBER_CLASS { self.process_id } else { 0 }
    }

    fn minimised(&self) -> bool {
        let Some(clients) = request("j/clients") else { return false };
        let Some(list) = array(&clients) else { return false };
        for client in list {
            let Some(class) = field(client, "class").and_then(text) else { continue };
            if class != SOBER_CLASS {
                continue;
            }
            let hidden = field(client, "hidden").and_then(flag).unwrap_or(false);
            let mapped = field(client, "mapped").and_then(flag).unwrap_or(true);
            return hidden || !mapped;
        }
        false
    }
}