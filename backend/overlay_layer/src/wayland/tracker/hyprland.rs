// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use super::{GameInfo, SoberRect, WindowTracker};
use std::collections::HashMap;
use std::io::{Read, Write};
use std::os::unix::net::UnixStream;
use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};
use tinyjson::JsonValue;

const SOBER_CLASS: &str = "org.vinegarhq.Sober";

const MONITORS_TTL: Duration = Duration::from_secs(1);

static MONITORS: Mutex<Option<(Instant, Arc<Vec<JsonValue>>)>> = Mutex::new(None);

fn socket_path(name: &str) -> Option<PathBuf> {
    let runtime = std::env::var_os("XDG_RUNTIME_DIR")?;
    let signature = std::env::var_os("HYPRLAND_INSTANCE_SIGNATURE")?;
    Some(
        PathBuf::from(runtime)
            .join("hypr")
            .join(signature)
            .join(name),
    )
}

fn exchange(payload: &[u8]) -> Option<String> {
    let mut stream = UnixStream::connect(socket_path(".socket.sock")?).ok()?;
    stream.write_all(payload).ok()?;
    let mut reply = String::new();
    stream.read_to_string(&mut reply).ok()?;
    Some(reply)
}

fn request(command: &str) -> Option<JsonValue> {
    exchange(command.as_bytes())?.parse::<JsonValue>().ok()
}

fn field<'a>(value: &'a JsonValue, key: &str) -> Option<&'a JsonValue> {
    value.get::<HashMap<String, JsonValue>>()?.get(key)
}

fn array(value: &JsonValue) -> Option<&Vec<JsonValue>> {
    value.get::<Vec<JsonValue>>()
}

fn int(value: &JsonValue) -> Option<i64> {
    value.get::<f64>().map(|n| *n as i64)
}

fn flag(value: &JsonValue) -> Option<bool> {
    value.get::<bool>().copied()
}

fn text(value: &JsonValue) -> Option<&str> {
    value.get::<String>().map(|s| s.as_str())
}

fn rect_for(client: &JsonValue, monitors: &[JsonValue]) -> Option<SoberRect> {
    let monitor_id = int(field(client, "monitor")?)?;
    let workspace_id = int(field(field(client, "workspace")?, "id")?)?;
    let monitor = monitors
        .iter()
        .find(|m| field(m, "id").and_then(int) == Some(monitor_id))?;

    if int(field(field(monitor, "activeWorkspace")?, "id")?)? != workspace_id {
        return None;
    }

    let monitor_x = int(field(monitor, "x")?)?;
    let monitor_y = int(field(monitor, "y")?)?;
    let at = array(field(client, "at")?)?;
    let size = array(field(client, "size")?)?;

    Some(SoberRect {
        monitor: monitor_id,
        x: (int(at.first()?)? - monitor_x) as i32,
        y: (int(at.get(1)?)? - monitor_y) as i32,
        width: int(size.first()?)? as i32,
        height: int(size.get(1)?)? as i32,
    })
}

fn is_sober(client: &JsonValue) -> bool {
    text(field(client, "class").unwrap_or(&JsonValue::Null)) == Some(SOBER_CLASS)
}

fn monitors() -> Option<Arc<Vec<JsonValue>>> {
    let mut cached = MONITORS
        .lock()
        .unwrap_or_else(|poisoned| poisoned.into_inner());

    if let Some((at, list)) = cached.as_ref() {
        if at.elapsed() < MONITORS_TTL {
            return Some(list.clone());
        }
    }

    let list = Arc::new(request("j/monitors").and_then(|value| array(&value).cloned())?);

    *cached = Some((Instant::now(), list.clone()));

    Some(list)
}

fn game_info() -> GameInfo {
    let Some(clients) = request("j/clients") else {
        return GameInfo::default();
    };
    let monitors = monitors();

    let active = request("j/activewindow")
        .map(|value| text(field(&value, "class").unwrap_or(&JsonValue::Null)) == Some(SOBER_CLASS))
        .unwrap_or(false);

    let Some(list) = array(&clients) else {
        return GameInfo::default();
    };

    let mut best: Option<GameInfo> = None;
    let mut best_area = -1i64;

    for client in list {
        if !is_sober(client) {
            continue;
        }

        if flag(field(client, "mapped").unwrap_or(&JsonValue::Null)) != Some(true) {
            continue;
        }

        let hidden = field(client, "hidden").and_then(flag) == Some(true);
        let fullscreen = field(client, "fullscreen").and_then(int).unwrap_or(0) > 0;
        let rect = monitors
            .as_deref()
            .and_then(|monitors| rect_for(client, monitors))
            .filter(|rect| rect.width > 0 && rect.height > 0);

        let candidate = GameInfo {
            rect,
            present: true,
            visible: !hidden && rect.is_some(),
            foreground: active && !hidden,
            fullscreen,
        };

        let area = rect.map(|r| (r.width as i64) * (r.height as i64)).unwrap_or(0);

        if best.is_none() || area > best_area {
            best_area = area;
            best = Some(candidate);
        }
    }

    best.unwrap_or_default()
}

fn focus_game() -> bool {
    exchange(b"dispatch focuswindow class:org.vinegarhq.Sober")
        .map(|reply| reply.trim().eq_ignore_ascii_case("ok") || reply.is_empty())
        .unwrap_or(false)
}

pub struct Hyprland;

impl WindowTracker for Hyprland {
    fn game_info(&self) -> GameInfo {
        game_info()
    }

    fn focus_game(&self) -> bool {
        focus_game()
    }
}
