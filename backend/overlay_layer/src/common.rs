use std::collections::VecDeque;
use std::sync::{Arc, Mutex, MutexGuard};
use std::time::Instant;

pub fn lock<T>(mutex: &Mutex<T>) -> MutexGuard<'_, T> {
    mutex.lock().unwrap_or_else(|poisoned| poisoned.into_inner())
}

pub const KIND_ENTER: i32 = 0;
pub const KIND_LEAVE: i32 = 1;
pub const KIND_MOTION: i32 = 2;
pub const KIND_BUTTON_DOWN: i32 = 3;
pub const KIND_BUTTON_UP: i32 = 4;
pub const KIND_AXIS: i32 = 5;
pub const KIND_KEY_DOWN: i32 = 6;
pub const KIND_KEY_UP: i32 = 7;
pub const KIND_HOTKEY: i32 = 8;
pub const KIND_FOREGROUND: i32 = 9;
pub const KIND_TEXT: i32 = 10;

pub const TEXT_LEN: usize = 64;
pub const SURFACE_GAME: usize = 0;
pub const SURFACE_KEY: usize = 1;
pub const SURFACE_SEARCH: usize = 2;
pub const SURFACE_COUNT: usize = 3;

#[repr(C)]
#[derive(Clone, Copy)]
pub struct InputEvent {
    pub kind: i32,
    pub code: u32,
    pub keysym: u32,
    pub x: f32,
    pub y: f32,
    pub dx: f32,
    pub dy: f32,
    pub text: [u8; TEXT_LEN],
}

impl Default for InputEvent {
    fn default() -> Self {
        Self {
            kind: 0, code: 0, keysym: 0,
            x: 0.0, y: 0.0, dx: 0.0, dy: 0.0,
            text: [0; TEXT_LEN],
        }
    }
}

impl InputEvent {
    pub fn with_text(mut self, value: &str) -> Self {
        let bytes = value.as_bytes();
        let len = bytes.len().min(TEXT_LEN - 1);
        self.text[..len].copy_from_slice(&bytes[..len]);
        self
    }
}

#[repr(C)]
#[derive(Clone, Copy, Default, PartialEq)]
pub struct GameRect {
    pub x: i32,
    pub y: i32,
    pub width: i32,
    pub height: i32,
    pub valid: i32,
}

pub(crate) struct Frame {
    pub width: i32,
    pub height: i32,
    pub pixels: Vec<u8>,
}

impl Frame {
    fn new() -> Frame {
        Frame { width: 0, height: 0, pixels: Vec::new() }
    }
}

pub(crate) struct Shared {
    input: Mutex<VecDeque<InputEvent>>,
    frames: Mutex<Vec<Frame>>,
    rect: Mutex<GameRect>,
    foreground_pid: Mutex<i32>,
    minimised: Mutex<bool>,
    logical: Mutex<Vec<(i32, i32)>>,
    scale: Mutex<Vec<i32>>,
    focus_known: Mutex<bool>,
    own_focus: Mutex<bool>,
    last_hotkey: Mutex<(u32, Instant)>,
}

impl Shared {
    pub fn new() -> Shared {
        let mut frames = Vec::with_capacity(SURFACE_COUNT);
        for _ in 0..SURFACE_COUNT {
            frames.push(Frame::new());
        }
        Shared {
            input: Mutex::new(VecDeque::new()),
            frames: Mutex::new(frames),
            rect: Mutex::new(GameRect::default()),
            foreground_pid: Mutex::new(0),
            minimised: Mutex::new(true),
            logical: Mutex::new(vec![(0, 0); SURFACE_COUNT]),
            scale: Mutex::new(vec![1; SURFACE_COUNT]),
            focus_known: Mutex::new(false),
            own_focus: Mutex::new(false),
            last_hotkey: Mutex::new((u32::MAX, Instant::now())),
        }
    }

    pub fn size(&self, surface: usize) -> (i32, i32) {
        let logical = lock(&self.logical);
        logical.get(surface).copied().unwrap_or((0, 0))
    }

    pub fn set_size(&self, surface: usize, width: i32, height: i32) {
        let mut logical = lock(&self.logical);
        if let Some(entry) = logical.get_mut(surface) {
            *entry = (width, height);
        }
    }

    pub fn set_scale(&self, surface: usize, value: i32) {
        let mut scale = lock(&self.scale);
        if let Some(entry) = scale.get_mut(surface) {
            *entry = value.max(1);
        }
    }

    pub fn scale(&self, surface: usize) -> i32 {
        let scale = lock(&self.scale);
        scale.get(surface).copied().unwrap_or(1).max(1)
    }

    pub fn submit(&self, surface: usize, pixels: &[u8], width: i32, height: i32, stride: i32) {
        if width <= 0 || height <= 0 || stride < width * 4 {
            return;
        }
        let row_bytes = width as usize * 4;
        let needed = stride as usize * (height as usize - 1) + row_bytes;
        if pixels.len() < needed {
            return;
        }
        let mut frames = lock(&self.frames);
        let Some(frame) = frames.get_mut(surface) else { return };
        frame.width = width;
        frame.height = height;
        frame.pixels.resize(row_bytes * height as usize, 0);
        for row in 0..height as usize {
            let src = row * stride as usize;
            let dst = row * row_bytes;
            frame.pixels[dst..dst + row_bytes].copy_from_slice(&pixels[src..src + row_bytes]);
        }
    }

    pub fn copy_frame_into(&self, surface: usize, target: &mut [u8], target_stride: usize, target_width: i32, target_height: i32) -> bool {
        let frames = lock(&self.frames);
        let Some(frame) = frames.get(surface) else { return false };
        if frame.width <= 0 || frame.height <= 0 {
            return false;
        }
        let copy_width = frame.width.min(target_width).max(0) as usize;
        let copy_height = frame.height.min(target_height).max(0) as usize;
        let src_stride = frame.width as usize * 4;
        let row_bytes = copy_width * 4;
        for row in 0..copy_height {
            let src = row * src_stride;
            let dst = row * target_stride;
            target[dst..dst + row_bytes].copy_from_slice(&frame.pixels[src..src + row_bytes]);
        }
        true
    }

    pub fn set_rect(&self, rect: GameRect) { *lock(&self.rect) = rect; }
    pub fn rect(&self) -> GameRect { *lock(&self.rect) }
    pub fn set_foreground(&self, pid: i32) { *lock(&self.foreground_pid) = pid; }
    pub fn foreground(&self) -> i32 { *lock(&self.foreground_pid) }
    pub fn set_minimised(&self, value: bool) { *lock(&self.minimised) = value; }
    pub fn minimised(&self) -> bool { *lock(&self.minimised) }
    pub fn set_focus_known(&self, value: bool) { *lock(&self.focus_known) = value; }
    pub fn focus_known(&self) -> bool { *lock(&self.focus_known) }
    pub fn set_own_focus(&self, value: bool) { *lock(&self.own_focus) = value; }
    pub fn own_focus(&self) -> bool { *lock(&self.own_focus) }

    pub fn push_input(&self, event: InputEvent) {
        if event.kind == KIND_HOTKEY {
            let mut last = lock(&self.last_hotkey);
            if last.0 == event.code && last.1.elapsed().as_millis() < 250 {
                return;
            }
            *last = (event.code, Instant::now());
        }
        let mut queue = lock(&self.input);
        if queue.len() >= 2048 {
            queue.pop_front();
        }
        queue.push_back(event);
    }

    pub fn poll_input(&self, out: &mut [InputEvent]) -> usize {
        let mut queue = lock(&self.input);
        let mut count = 0;
        while count < out.len() {
            match queue.pop_front() {
                Some(event) => { out[count] = event; count += 1; }
                None => break,
            }
        }
        count
    }
}

pub(crate) type SharedRef = Arc<Shared>;