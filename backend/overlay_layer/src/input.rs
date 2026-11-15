use std::collections::{HashMap, HashSet, VecDeque};
use std::fs::File;
use std::io::Read;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex, MutexGuard};
use std::thread;
use std::time::{Duration, SystemTime, UNIX_EPOCH};

const EV_KEY: u16 = 1;
const KEY_MAX: usize = 0x300;
const KEY_BYTES: usize = KEY_MAX / 8;
const BTN_LEFT: u16 = 0x110;
const BTN_RIGHT: u16 = 0x111;
const KEY_LEFTCTRL: usize = 29;
const KEY_LEFTSHIFT: usize = 42;
const KEY_LEFTALT: usize = 56;
const KEY_RIGHTCTRL: usize = 97;
const KEY_RIGHTALT: usize = 100;
const KEY_RIGHTMETA: usize = 126;
const KEY_RIGHTSHIFT: usize = 54;
const KEY_LEFTMETA: usize = 125;

fn lock<T>(mutex: &Mutex<T>) -> MutexGuard<'_, T> {
    mutex
        .lock()
        .unwrap_or_else(|poisoned| poisoned.into_inner())
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct KeyState {
    pub keys: [u8; KEY_BYTES],
    pub mouse: u32,
    pub left_cps: f32,
    pub right_cps: f32,
}

impl Default for KeyState {
    fn default() -> Self {
        KeyState {
            keys: [0; KEY_BYTES],
            mouse: 0,
            left_cps: 0.0,
            right_cps: 0.0,
        }
    }
}

struct Hotkey {
    code: u16,
    modifiers: u32,
    down: bool,
}

struct Input {
    keys: [u8; KEY_BYTES],
    mouse: u32,
    hotkeys: HashMap<i32, Hotkey>,
    pending: VecDeque<i32>,
    left_score: f64,
    right_score: f64,
    score_time: i64,
}

impl Input {
    fn new() -> Input {
        Input {
            keys: [0; KEY_BYTES],
            mouse: 0,
            hotkeys: HashMap::new(),
            pending: VecDeque::new(),
            left_score: 0.0,
            right_score: 0.0,
            score_time: 0,
        }
    }
}

pub struct GlobalInput {
    state: Arc<Mutex<Input>>,
    stop: Arc<AtomicBool>,
}

impl GlobalInput {
    pub fn start() -> GlobalInput {
        let state = Arc::new(Mutex::new(Input::new()));
        let stop = Arc::new(AtomicBool::new(false));
        let active = Arc::new(Mutex::new(HashSet::<String>::new()));

        let thread_state = state.clone();
        let thread_stop = stop.clone();
        let thread_active = active.clone();

        thread::Builder::new()
            .name("froststrap-input".into())
            .spawn(move || scan_loop(thread_state, thread_stop, thread_active))
            .ok();

        GlobalInput { state, stop }
    }

    pub fn register(&self, id: i32, code: u16, modifiers: u32) {
        lock(&self.state).hotkeys.insert(
            id,
            Hotkey {
                code,
                modifiers,
                down: false,
            },
        );
    }

    pub fn unregister(&self, id: i32) {
        lock(&self.state).hotkeys.remove(&id);
    }

    pub fn poll(&self) -> i32 {
        lock(&self.state).pending.pop_front().unwrap_or(-1)
    }

    pub fn snapshot(&self) -> KeyState {
        let mut input = lock(&self.state);

        decay(&mut input);

        KeyState {
            keys: input.keys,
            mouse: input.mouse,
            left_cps: input.left_score as f32,
            right_cps: input.right_score as f32,
        }
    }

    pub fn stop(&self) {
        self.stop.store(true, Ordering::Relaxed);
    }
}

fn scan_loop(state: Arc<Mutex<Input>>, stop: Arc<AtomicBool>, active: Arc<Mutex<HashSet<String>>>) {
    while !stop.load(Ordering::Relaxed) {
        if let Ok(entries) = std::fs::read_dir("/dev/input") {
            for entry in entries.flatten() {
                let name = entry.file_name().to_string_lossy().into_owned();

                if !name.starts_with("event") {
                    continue;
                }

                let path = entry.path().to_string_lossy().into_owned();

                if !lock(&active).insert(path.clone()) {
                    continue;
                }

                let device_state = state.clone();
                let device_active = active.clone();
                let device_path = path.clone();

                thread::Builder::new()
                    .name("froststrap-input-dev".into())
                    .spawn(move || {
                        read_device(&device_path, device_state);
                        lock(&device_active).remove(&device_path);
                    })
                    .ok();
            }
        }

        thread::sleep(Duration::from_millis(3000));
    }
}

fn read_device(path: &str, state: Arc<Mutex<Input>>) {
    let Ok(mut file) = File::open(path) else {
        return;
    };

    let mut buffer = [0u8; 24 * 64];

    loop {
        match file.read(&mut buffer) {
            Ok(0) => break,
            Ok(count) => {
                let mut offset = 0;

                while offset + 24 <= count {
                    let kind = u16::from_ne_bytes([buffer[offset + 16], buffer[offset + 17]]);

                    if kind == EV_KEY {
                        let code =
                            u16::from_ne_bytes([buffer[offset + 18], buffer[offset + 19]]);
                        let value = i32::from_ne_bytes([
                            buffer[offset + 20],
                            buffer[offset + 21],
                            buffer[offset + 22],
                            buffer[offset + 23],
                        ]);

                        handle_key(&state, code, value);
                    }

                    offset += 24;
                }
            }
            Err(_) => break,
        }
    }
}

fn handle_key(state: &Arc<Mutex<Input>>, code: u16, value: i32) {
    let mut input = lock(state);

    if (code as usize) < KEY_MAX {
        set_bit(&mut input.keys, code as usize, value != 0);
    }

    match code {
        BTN_LEFT => set_mouse(&mut input, 0, value != 0),
        BTN_RIGHT => set_mouse(&mut input, 1, value != 0),
        _ => {}
    }

    if value == 1 {
        let modifiers = modifier_bits(&input.keys);
        let mut fired: Vec<i32> = Vec::new();

        for (id, hotkey) in input.hotkeys.iter_mut() {
            if hotkey.code == code && hotkey.modifiers == modifiers && !hotkey.down {
                hotkey.down = true;
                fired.push(*id);
            }
        }

        for id in fired {
            if input.pending.len() >= 256 {
                input.pending.pop_front();
            }
            input.pending.push_back(id);
        }

        if code == BTN_LEFT {
            add_click(&mut input, true);
        } else if code == BTN_RIGHT {
            add_click(&mut input, false);
        }
    } else if value == 0 {
        for hotkey in input.hotkeys.values_mut() {
            if hotkey.code == code {
                hotkey.down = false;
            }
        }
    }
}

fn add_click(input: &mut Input, left: bool) {
    decay(input);

    if left {
        input.left_score += 1.0;
    } else {
        input.right_score += 1.0;
    }
}

fn decay(input: &mut Input) {
    let now = now_ms();

    if input.score_time == 0 {
        input.score_time = now;
        return;
    }

    let elapsed = (now - input.score_time) as f64 / 1000.0;

    if elapsed <= 0.0 {
        return;
    }

    let factor = (-elapsed).exp();

    input.left_score *= factor;
    input.right_score *= factor;
    input.score_time = now;
}

fn modifier_bits(keys: &[u8]) -> u32 {
    let alt = get_bit(keys, KEY_LEFTALT) || get_bit(keys, KEY_RIGHTALT);
    let control = get_bit(keys, KEY_LEFTCTRL) || get_bit(keys, KEY_RIGHTCTRL);
    let shift = get_bit(keys, KEY_LEFTSHIFT) || get_bit(keys, KEY_RIGHTSHIFT);
    let logo = get_bit(keys, KEY_LEFTMETA) || get_bit(keys, KEY_RIGHTMETA);

    (alt as u32) | ((control as u32) << 1) | ((shift as u32) << 2) | ((logo as u32) << 3)
}

fn set_bit(keys: &mut [u8], code: usize, value: bool) {
    if code >= KEY_MAX {
        return;
    }

    let mask = 1u8 << (code % 8);
    let slot = &mut keys[code / 8];

    if value {
        *slot |= mask;
    } else {
        *slot &= !mask;
    }
}

fn get_bit(keys: &[u8], code: usize) -> bool {
    if code >= KEY_MAX {
        return false;
    }

    keys[code / 8] & (1u8 << (code % 8)) != 0
}

fn set_mouse(input: &mut Input, bit: u32, value: bool) {
    if value {
        input.mouse |= 1 << bit;
    } else {
        input.mouse &= !(1 << bit);
    }
}

fn now_ms() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| duration.as_millis() as i64)
        .unwrap_or(0)
}
