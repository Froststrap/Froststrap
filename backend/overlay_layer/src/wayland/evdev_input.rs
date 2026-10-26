use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Mutex;
use std::thread;
use std::time::{Duration, Instant};

use evdev::{Device, EventType, Key};

use crate::common::{
    InputEvent, SharedRef,
    KIND_BUTTON_DOWN, KIND_BUTTON_UP, KIND_HOTKEY, KIND_KEY_DOWN, KIND_KEY_UP,
};

const MOD_SHIFT: u32 = 1;
const MOD_CONTROL: u32 = 4;
const MOD_ALT: u32 = 8;
const MOD_META: u32 = 64;

const POLL_INTERVAL_MS: u64 = 4;
const REOBSERVE_INTERVAL_MS: u64 = 2000;

static MONITORING: AtomicBool = AtomicBool::new(false);
static STARTED: AtomicBool = AtomicBool::new(false);

struct Hotkey {
    id: i32,
    keycode: u32,
    modifiers: u32,
}

static HOTKEYS: Mutex<Vec<Hotkey>> = Mutex::new(Vec::new());

pub fn register_hotkey(id: i32, evdev_keycode: u32, modifiers: u32) {
    if let Ok(mut list) = HOTKEYS.lock() {
        list.retain(|h| h.id != id);
        if evdev_keycode > 0 {
            list.push(Hotkey { id, keycode: evdev_keycode, modifiers });
        }
    }
}

pub fn unregister_hotkey(id: i32) {
    if let Ok(mut list) = HOTKEYS.lock() {
        list.retain(|h| h.id != id);
    }
}

pub fn start(shared: SharedRef) {
    if STARTED.swap(true, Ordering::SeqCst) {
        return;
    }
    let _ = thread::Builder::new()
        .name("evdev-input".into())
        .spawn(move || monitor(shared));
}

pub fn set_enabled(enabled: bool) {
    MONITORING.store(enabled, Ordering::SeqCst);
}

enum DeviceKind {
    Keyboard,
    Pointer,
}

struct OpenDevice {
    device: Device,
    kind: DeviceKind,
}

fn classify(device: &Device) -> Option<DeviceKind> {
    if let Some(keys) = device.supported_keys() {
        if keys.contains(Key::KEY_A) && keys.contains(Key::KEY_Z) {
            return Some(DeviceKind::Keyboard);
        }
        if keys.contains(Key::BTN_LEFT) && keys.contains(Key::BTN_RIGHT) {
            return Some(DeviceKind::Pointer);
        }
    }
    None
}

fn open_devices() -> Vec<OpenDevice> {
    let mut result = Vec::new();
    let mut denied = 0usize;

    for (path, device) in evdev::enumerate() {
        let Some(kind) = classify(&device) else { continue };
        match Device::open(&path) {
            Ok(dev) => result.push(OpenDevice { device: dev, kind }),
            Err(err) => {
                denied += 1;
                eprintln!(
                    "evdev: cannot open {} ({err}); run: sudo usermod -a -G input $USER, then relog",
                    path.display()
                );
            }
        }
    }

    if result.is_empty() && denied > 0 {
        eprintln!("evdev: 0 readable devices; 'input' group membership is required");
    }

    result
}

fn monitor(shared: SharedRef) {
    let mut devices = open_devices();
    let mut modifiers = 0u32;
    let mut last_observe = Instant::now();

    loop {
        if devices.is_empty()
            || last_observe.elapsed() >= Duration::from_millis(REOBSERVE_INTERVAL_MS)
        {
            last_observe = Instant::now();
            devices = open_devices();
            if devices.is_empty() {
                thread::sleep(Duration::from_secs(2));
                continue;
            }
        }

        let mut got_any = false;

        for opened in &mut devices {
            let Ok(events) = opened.device.fetch_events() else { continue };

            for event in events {
                got_any = true;

                if event.event_type() != EventType::KEY {
                    continue;
                }

                let code = event.code();
                let value = event.value();
                let pressed = value == 1;
                let released = value == 0;

                if !pressed && !released {
                    continue;
                }

                match opened.kind {
                    DeviceKind::Keyboard => {
                        if let Some(bit) = modifier_bit(Key::new(code)) {
                            if pressed {
                                modifiers |= bit;
                            } else {
                                modifiers &= !bit;
                            }
                            continue;
                        }

                        if pressed {
                            let matched = HOTKEYS
                                .lock()
                                .ok()
                                .and_then(|list| {
                                    list.iter()
                                        .find(|h| {
                                            h.keycode == code as u32
                                                && h.modifiers == modifiers
                                        })
                                        .map(|h| h.id)
                                });

                            if let Some(id) = matched
                                && !super::portal::available()
                            {
                                shared.push_input(InputEvent {
                                    kind: KIND_HOTKEY,
                                    code: id as u32,
                                    ..InputEvent::default()
                                });
                            }
                        }

                        if !MONITORING.load(Ordering::Relaxed) {
                            continue;
                        }

                        shared.push_input(InputEvent {
                            kind: if pressed { KIND_KEY_DOWN } else { KIND_KEY_UP },
                            code: x11_keycode(Key::new(code)),
                            ..InputEvent::default()
                        });
                    }

                    DeviceKind::Pointer => {
                        if !MONITORING.load(Ordering::Relaxed) {
                            continue;
                        }

                        let button = match Key::new(code) {
                            Key::BTN_LEFT => 1u32,
                            Key::BTN_MIDDLE => 2u32,
                            Key::BTN_RIGHT => 3u32,
                            _ => continue,
                        };

                        shared.push_input(InputEvent {
                            kind: if pressed { KIND_BUTTON_DOWN } else { KIND_BUTTON_UP },
                            code: button,
                            ..InputEvent::default()
                        });
                    }
                }
            }
        }

        if !got_any {
            thread::sleep(Duration::from_millis(POLL_INTERVAL_MS));
        }
    }
}

fn modifier_bit(code: Key) -> Option<u32> {
    match code {
        Key::KEY_LEFTSHIFT | Key::KEY_RIGHTSHIFT => Some(MOD_SHIFT),
        Key::KEY_LEFTCTRL | Key::KEY_RIGHTCTRL => Some(MOD_CONTROL),
        Key::KEY_LEFTALT | Key::KEY_RIGHTALT => Some(MOD_ALT),
        Key::KEY_LEFTMETA | Key::KEY_RIGHTMETA => Some(MOD_META),
        _ => None,
    }
}

fn x11_keycode(code: Key) -> u32 {
    (code.0 as u32) + 8
}