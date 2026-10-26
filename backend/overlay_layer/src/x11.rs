use std::collections::HashMap;
use std::ffi::{c_char, c_void, CStr};
use std::os::raw::c_int;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::mpsc::{self, Receiver, RecvTimeoutError, Sender};
use std::sync::{Mutex, Once, OnceLock};
use std::thread::{self, JoinHandle};
use std::time::{Duration, Instant};

use x11_dl::{xfixes, xlib, xrecord};

use crate::common::{
    GameRect, InputEvent, SharedRef,
    KIND_BUTTON_DOWN, KIND_BUTTON_UP, KIND_FOREGROUND, KIND_HOTKEY, KIND_KEY_DOWN, KIND_KEY_UP,
};

const NET_ACTIVE_WINDOW: &[u8] = b"_NET_ACTIVE_WINDOW\0";
const NET_CLIENT_LIST: &[u8] = b"_NET_CLIENT_LIST\0";
const NET_WM_PID: &[u8] = b"_NET_WM_PID\0";
const NET_WM_STATE: &[u8] = b"_NET_WM_STATE\0";
const NET_WM_STATE_HIDDEN: &[u8] = b"_NET_WM_STATE_HIDDEN\0";

const SHAPE_INPUT: c_int = 2;
const XA_WINDOW: xlib::Atom = 33;
const XA_CARDINAL: xlib::Atom = 6;
const XA_ATOM: xlib::Atom = 4;

const XI_MOD_SHIFT: u32 = 1;
const XI_MOD_CONTROL: u32 = 4;
const XI_MOD_MOD1: u32 = 8;
const XI_MOD_MOD4: u32 = 64;

const POLL_INTERVAL: Duration = Duration::from_millis(16);
const SYNC_INTERVAL: Duration = Duration::from_millis(100);

static X11_INIT: Once = Once::new();
static RECORD_SHARED: Mutex<Option<SharedRef>> = Mutex::new(None);
static RECORD_ENABLED: AtomicBool = AtomicBool::new(false);
static RECORD_SPAWNED: AtomicBool = AtomicBool::new(false);
static RECORD_FREE_DATA: OnceLock<unsafe extern "C" fn(*mut xrecord::XRecordInterceptData)> = OnceLock::new();

pub enum Message {
    RegisterHotkey {
        id: i32,
        keysym: u32,
        modifiers: u32,
        reply: Sender<i32>,
    },
    UnregisterHotkey {
        id: i32,
    },
    SetKeyMonitoring {
        enabled: bool,
    },
    SetInputRegion {
        window_id: u64,
        rects: Vec<[i32; 4]>,
        reply: Sender<i32>,
    },
    ClearInputRegion {
        window_id: u64,
        reply: Sender<i32>,
    },
    FocusGame {
        reply: Sender<i32>,
    },
    Shutdown,
}

pub struct Backend {
    shared: SharedRef,
    sender: Mutex<Sender<Message>>,
    thread: Mutex<Option<JoinHandle<()>>>,
}

impl Backend {
    pub(crate) fn start(pid: i32, shared: SharedRef) -> Result<Self, String> {
        let (tx, rx) = mpsc::channel();
        let thread_shared = shared.clone();

        let thread = thread::Builder::new()
            .name("x11-overlay".into())
            .spawn(move || {
                if let Err(err) = run(pid, thread_shared, rx) {
                    eprintln!("x11-overlay: {err}");
                }
            })
            .map_err(|e| format!("spawn x11 thread: {e}"))?;

        Ok(Backend {
            shared,
            sender: Mutex::new(tx),
            thread: Mutex::new(Some(thread)),
        })
    }

    pub fn poll_input(&self, out: &mut [InputEvent]) -> usize {
        self.shared.poll_input(out)
    }

    pub fn register_hotkey(&self, id: i32, keysym: u32, modifiers: u32) -> i32 {
        let (tx, rx) = mpsc::channel();
        let msg = Message::RegisterHotkey { id, keysym, modifiers, reply: tx };
        if self.send(msg).is_err() {
            return -1;
        }
        rx.recv_timeout(Duration::from_secs(2)).unwrap_or(-1)
    }

    pub fn unregister_hotkey(&self, id: i32) {
        let _ = self.send(Message::UnregisterHotkey { id });
    }

    pub fn set_key_monitoring(&self, enabled: bool) -> i32 {
        if self.send(Message::SetKeyMonitoring { enabled }).is_err() {
            return -1;
        }
        0
    }

    pub fn set_input_region(&self, window_id: u64, rects: &[[i32; 4]]) -> i32 {
        let (tx, rx) = mpsc::channel();
        let msg = Message::SetInputRegion { window_id, rects: rects.to_vec(), reply: tx };
        if self.send(msg).is_err() {
            return -1;
        }
        rx.recv_timeout(Duration::from_secs(2)).unwrap_or(-1)
    }

    pub fn clear_input_region(&self, window_id: u64) -> i32 {
        let (tx, rx) = mpsc::channel();
        let msg = Message::ClearInputRegion { window_id, reply: tx };
        if self.send(msg).is_err() {
            return -1;
        }
        rx.recv_timeout(Duration::from_secs(2)).unwrap_or(-1)
    }

    pub fn focus_game(&self, _pid: i32) -> i32 {
        let (tx, rx) = mpsc::channel();
        if self.send(Message::FocusGame { reply: tx }).is_err() {
            return -1;
        }
        rx.recv_timeout(Duration::from_secs(2)).unwrap_or(-1)
    }

    pub fn is_foreground(&self, pid: i32) -> i32 {
        if self.shared.foreground() == pid { 1 } else { 0 }
    }

    pub fn current_foreground(&self) -> i32 {
        self.shared.foreground()
    }

    pub fn focus_known(&self) -> i32 {
        if self.shared.focus_known() { 1 } else { 0 }
    }

    pub fn is_minimised(&self) -> i32 {
        if self.shared.minimised() { 1 } else { 0 }
    }

    pub fn game_rect(&self) -> GameRect {
        self.shared.rect()
    }

    pub fn shutdown(self) {
        RECORD_ENABLED.store(false, Ordering::SeqCst);
        if let Ok(mut guard) = RECORD_SHARED.lock() {
            *guard = None;
        }

        let _ = self.send(Message::Shutdown);

        if let Ok(mut guard) = self.thread.lock() {
            if let Some(handle) = guard.take() {
                let _ = handle.join();
            }
        }
    }

    fn send(&self, msg: Message) -> Result<(), ()> {
        self.sender
            .lock()
            .map_err(|_| ())?
            .send(msg)
            .map_err(|_| ())
    }
}

struct State {
    xlib: xlib::Xlib,
    xfixes: xfixes::Xlib,
    display: *mut xlib::Display,
    root: xlib::Window,
    net_active_window: xlib::Atom,
    net_client_list: xlib::Atom,
    net_wm_pid: xlib::Atom,
    net_wm_state: xlib::Atom,
    net_wm_state_hidden: xlib::Atom,
    pid: i32,
    shared: SharedRef,
    hotkeys: HashMap<i32, (u32, u32)>,
    target: Option<xlib::Window>,
    last_foreground: i32,
    last_minimised: bool,
}

fn init_x11_threads() {
    X11_INIT.call_once(|| {
        if let Ok(xlib) = xlib::Xlib::open() {
            unsafe {
                (xlib.XInitThreads)();
            }
        }
    });
}

fn run(pid: i32, shared: SharedRef, receiver: Receiver<Message>) -> Result<(), String> {
    init_x11_threads();

    let xlib = xlib::Xlib::open().map_err(|e| format!("libX11: {e}"))?;
    let xfixes = xfixes::Xlib::open().map_err(|e| format!("libXfixes: {e}"))?;

    unsafe {
        let display = (xlib.XOpenDisplay)(std::ptr::null());
        if display.is_null() {
            return Err("XOpenDisplay failed".into());
        }

        let screen = (xlib.XDefaultScreen)(display);
        let root = (xlib.XRootWindow)(display, screen);

        let net_active_window = intern(&xlib, display, NET_ACTIVE_WINDOW)?;
        let net_client_list = intern(&xlib, display, NET_CLIENT_LIST)?;
        let net_wm_pid = intern(&xlib, display, NET_WM_PID)?;
        let net_wm_state = intern(&xlib, display, NET_WM_STATE)?;
        let net_wm_state_hidden = intern(&xlib, display, NET_WM_STATE_HIDDEN)?;

        (xlib.XSelectInput)(
            display,
            root,
            xlib::PropertyChangeMask | xlib::KeyPressMask,
        );

        let mut state = State {
            xlib,
            xfixes,
            display,
            root,
            net_active_window,
            net_client_list,
            net_wm_pid,
            net_wm_state,
            net_wm_state_hidden,
            pid,
            shared: shared.clone(),
            hotkeys: HashMap::new(),
            target: None,
            last_foreground: 0,
            last_minimised: true,
        };

        if let Ok(mut guard) = RECORD_SHARED.lock() {
            *guard = Some(shared.clone());
        }

        state.refresh_all();

        let mut last_sync = Instant::now();

        loop {
            match receiver.recv_timeout(POLL_INTERVAL) {
                Ok(msg) => {
                    if !state.handle(msg) {
                        break;
                    }
                    while let Ok(msg) = receiver.try_recv() {
                        if !state.handle(msg) {
                            state.cleanup();
                            return Ok(());
                        }
                    }
                }
                Err(RecvTimeoutError::Timeout) => {}
                Err(RecvTimeoutError::Disconnected) => break,
            }

            state.process_events();

            if last_sync.elapsed() >= SYNC_INTERVAL {
                last_sync = Instant::now();
                state.refresh_all();
            }
        }

        state.cleanup();
    }

    Ok(())
}

impl State {
    fn handle(&mut self, msg: Message) -> bool {
        match msg {
            Message::RegisterHotkey { id, keysym, modifiers, reply } => {
                let result = self.register_hotkey(id, keysym, modifiers);
                let _ = reply.send(result);
            }
            Message::UnregisterHotkey { id } => {
                self.unregister_hotkey(id);
            }
            Message::SetKeyMonitoring { enabled } => {
                self.set_key_monitoring(enabled);
            }
            Message::SetInputRegion { window_id, rects, reply } => {
                let result = self.set_input_region(window_id, &rects);
                let _ = reply.send(result);
            }
            Message::ClearInputRegion { window_id, reply } => {
                let result = self.clear_input_region(window_id);
                let _ = reply.send(result);
            }
            Message::FocusGame { reply } => {
                let result = self.focus_game();
                let _ = reply.send(result);
            }
            Message::Shutdown => return false,
        }
        true
    }

    fn register_hotkey(&mut self, id: i32, keysym: u32, modifiers: u32) -> i32 {
        unsafe {
            let keycode = (self.xlib.XKeysymToKeycode)(self.display, keysym as xlib::KeySym);
            if keycode == 0 {
                return -1;
            }

            let mods = translate_modifiers(modifiers);

            for extra in [
                0u32,
                xlib::LockMask,
                xlib::Mod2Mask,
                xlib::LockMask | xlib::Mod2Mask,
            ] {
                (self.xlib.XGrabKey)(
                    self.display,
                    keycode as c_int,
                    mods | extra,
                    self.root,
                    xlib::True as c_int,
                    xlib::GrabModeAsync as c_int,
                    xlib::GrabModeAsync as c_int,
                );
            }

            self.hotkeys.insert(id, (keycode as u32, mods));
            (self.xlib.XFlush)(self.display);

            0
        }
    }

    fn unregister_hotkey(&mut self, id: i32) {
        if let Some((keycode, mods)) = self.hotkeys.remove(&id) {
            unsafe {
                for extra in [
                    0u32,
                    xlib::LockMask,
                    xlib::Mod2Mask,
                    xlib::LockMask | xlib::Mod2Mask,
                ] {
                    (self.xlib.XUngrabKey)(
                        self.display,
                        keycode as c_int,
                        mods | extra,
                        self.root,
                    );
                }
                (self.xlib.XFlush)(self.display);
            }
        }
    }

    fn set_key_monitoring(&mut self, enabled: bool) {
        RECORD_ENABLED.store(enabled, Ordering::SeqCst);

        if enabled && !RECORD_SPAWNED.swap(true, Ordering::SeqCst) {
            let _ = thread::Builder::new()
                .name("x11-record".into())
                .spawn(run_record_thread);
        }
    }

    fn set_input_region(&mut self, window_id: u64, rects: &[[i32; 4]]) -> i32 {
        unsafe {
            let window = window_id as xlib::Window;
            if window == 0 {
                return -1;
            }

            let xrects: Vec<xlib::XRectangle> = rects
                .iter()
                .filter(|[_, _, w, h]| *w > 0 && *h > 0)
                .map(|[x, y, w, h]| xlib::XRectangle {
                    x: *x as i16,
                    y: *y as i16,
                    width: *w as u16,
                    height: *h as u16,
                })
                .collect();

            let region = if xrects.is_empty() {
                (self.xfixes.XFixesCreateRegion)(self.display, std::ptr::null_mut(), 0)
            } else {
                (self.xfixes.XFixesCreateRegion)(
                    self.display,
                    xrects.as_ptr() as *mut xlib::XRectangle,
                    xrects.len() as c_int,
                )
            };

            if region == 0 {
                return -1;
            }

            (self.xfixes.XFixesSetWindowShapeRegion)(
                self.display,
                window,
                SHAPE_INPUT,
                0,
                0,
                region,
            );

            (self.xfixes.XFixesDestroyRegion)(self.display, region);
            (self.xlib.XFlush)(self.display);

            0
        }
    }

    fn clear_input_region(&mut self, window_id: u64) -> i32 {
        unsafe {
            let window = window_id as xlib::Window;
            if window == 0 {
                return -1;
            }

            (self.xfixes.XFixesSetWindowShapeRegion)(
                self.display,
                window,
                SHAPE_INPUT,
                0,
                0,
                0,
            );
            (self.xlib.XFlush)(self.display);

            0
        }
    }

    fn focus_game(&mut self) -> i32 {
        if self.target.is_none() {
            self.refresh_target();
        }

        let target = match self.target {
            Some(t) => t,
            None => return -1,
        };

        unsafe {
            (self.xlib.XRaiseWindow)(self.display, target);
            (self.xlib.XSetInputFocus)(
                self.display,
                target,
                xlib::RevertToParent as c_int,
                xlib::CurrentTime as xlib::Time,
            );
            (self.xlib.XFlush)(self.display);
        }

        0
    }

    fn process_events(&mut self) {
        unsafe {
            let mut event: xlib::XEvent = std::mem::zeroed();

            while (self.xlib.XPending)(self.display) > 0 {
                (self.xlib.XNextEvent)(self.display, &mut event);

                match event.get_type() {
                    xlib::KeyPress => {
                        self.handle_key_press(&event);
                    }
                    xlib::PropertyNotify => {
                        let atom = event.property.atom;
                        if atom == self.net_active_window {
                            self.refresh_foreground();
                        } else if atom == self.net_client_list || atom == self.net_wm_state {
                            self.refresh_target();
                            self.refresh_rect();
                            self.refresh_minimised();
                        }
                    }
                    _ => {}
                }
            }
        }
    }

    fn handle_key_press(&self, event: &xlib::XEvent) {
        let ev = unsafe { &event.key };
        let mask = (ev.state as u32) & !((xlib::LockMask | xlib::Mod2Mask) as u32);
        let keycode = ev.keycode as u32;

        for (id, (kc, km)) in &self.hotkeys {
            if *kc == keycode && *km == mask {
                self.shared.push_input(InputEvent {
                    kind: KIND_HOTKEY,
                    code: *id as u32,
                    ..Default::default()
                });
                return;
            }
        }
    }

    fn refresh_all(&mut self) {
        self.refresh_target();
        self.refresh_foreground();
        self.refresh_rect();
        self.refresh_minimised();
    }

    fn refresh_target(&mut self) {
        let found = self.find_game_window();

        match found {
            Some(win) => {
                if self.target != Some(win) {
                    self.target = Some(win);
                    self.shared.push_input(InputEvent {
                        kind: KIND_FOREGROUND,
                        code: win as u32,
                        ..Default::default()
                    });
                }
            }
            None => {
                self.target = None;
            }
        }

        self.shared.set_focus_known(self.target.is_some());
    }

    fn find_game_window(&self) -> Option<xlib::Window> {
        unsafe {
            let mut actual_type: xlib::Atom = 0;
            let mut actual_format: c_int = 0;
            let mut count: std::os::raw::c_ulong = 0;
            let mut remaining: std::os::raw::c_ulong = 0;
            let mut data: *mut u8 = std::ptr::null_mut();

            let status = (self.xlib.XGetWindowProperty)(
                self.display,
                self.root,
                self.net_client_list,
                0,
                8192,
                0,
                XA_WINDOW,
                &mut actual_type,
                &mut actual_format,
                &mut count,
                &mut remaining,
                &mut data,
            );

            if status != 0 || data.is_null() || count == 0 {
                if !data.is_null() {
                    (self.xlib.XFree)(data as *mut c_void);
                }
                return None;
            }

            let windows = std::slice::from_raw_parts(data as *const xlib::Window, count as usize);
            let mut found = None;

            for &win in windows {
                if self.read_window_pid(win) == self.pid {
                    found = Some(win);
                    break;
                }
            }

            (self.xlib.XFree)(data as *mut c_void);
            found
        }
    }

    fn read_window_pid(&self, window: xlib::Window) -> i32 {
        unsafe {
            let mut actual_type: xlib::Atom = 0;
            let mut actual_format: c_int = 0;
            let mut count: std::os::raw::c_ulong = 0;
            let mut remaining: std::os::raw::c_ulong = 0;
            let mut data: *mut u8 = std::ptr::null_mut();

            let status = (self.xlib.XGetWindowProperty)(
                self.display,
                window,
                self.net_wm_pid,
                0,
                1,
                0,
                XA_CARDINAL,
                &mut actual_type,
                &mut actual_format,
                &mut count,
                &mut remaining,
                &mut data,
            );

            if status != 0 || data.is_null() || count == 0 {
                if !data.is_null() {
                    (self.xlib.XFree)(data as *mut c_void);
                }
                return 0;
            }

            let pid = *(data as *const u32) as i32;
            (self.xlib.XFree)(data as *mut c_void);
            pid
        }
    }

    fn refresh_foreground(&mut self) {
        let pid = unsafe {
            let mut actual_type: xlib::Atom = 0;
            let mut actual_format: c_int = 0;
            let mut count: std::os::raw::c_ulong = 0;
            let mut remaining: std::os::raw::c_ulong = 0;
            let mut data: *mut u8 = std::ptr::null_mut();

            let status = (self.xlib.XGetWindowProperty)(
                self.display,
                self.root,
                self.net_active_window,
                0,
                1,
                0,
                XA_WINDOW,
                &mut actual_type,
                &mut actual_format,
                &mut count,
                &mut remaining,
                &mut data,
            );

            if status != 0 || data.is_null() || count == 0 {
                if !data.is_null() {
                    (self.xlib.XFree)(data as *mut c_void);
                }
                0
            } else {
                let active = *(data as *const xlib::Window);
                (self.xlib.XFree)(data as *mut c_void);

                if active == 0 {
                    0
                } else {
                    self.read_window_pid(active)
                }
            }
        };

        if pid != self.last_foreground {
            self.last_foreground = pid;
            self.shared.set_foreground(pid);
        }
    }

    fn refresh_rect(&mut self) {
        let rect = match self.target {
            Some(win) => self.read_rect(win),
            None => GameRect::default(),
        };

        self.shared.set_rect(rect);
    }

    fn read_rect(&self, window: xlib::Window) -> GameRect {
        unsafe {
            let mut attributes: xlib::XWindowAttributes = std::mem::zeroed();

            if (self.xlib.XGetWindowAttributes)(self.display, window, &mut attributes) == 0 {
                return GameRect::default();
            }

            if attributes.map_state != xlib::IsViewable {
                return GameRect::default();
            }

            let mut root_x: c_int = 0;
            let mut root_y: c_int = 0;
            let mut child: xlib::Window = 0;

            if (self.xlib.XTranslateCoordinates)(
                self.display,
                window,
                self.root,
                0,
                0,
                &mut root_x,
                &mut root_y,
                &mut child,
            ) == 0
            {
                return GameRect::default();
            }

            GameRect {
                x: root_x,
                y: root_y,
                width: attributes.width,
                height: attributes.height,
                valid: 1,
            }
        }
    }

    fn refresh_minimised(&mut self) {
        let minimised = match self.target {
            Some(win) => self.is_window_minimised(win),
            None => true,
        };

        if minimised != self.last_minimised {
            self.last_minimised = minimised;
            self.shared.set_minimised(minimised);
        }
    }

    fn is_window_minimised(&self, window: xlib::Window) -> bool {
        unsafe {
            let mut attributes: xlib::XWindowAttributes = std::mem::zeroed();

            if (self.xlib.XGetWindowAttributes)(self.display, window, &mut attributes) == 0 {
                return true;
            }

            if attributes.map_state != xlib::IsViewable {
                return true;
            }

            self.has_wm_state(window, self.net_wm_state_hidden)
        }
    }

    fn has_wm_state(&self, window: xlib::Window, wanted: xlib::Atom) -> bool {
        unsafe {
            let mut actual_type: xlib::Atom = 0;
            let mut actual_format: c_int = 0;
            let mut count: std::os::raw::c_ulong = 0;
            let mut remaining: std::os::raw::c_ulong = 0;
            let mut data: *mut u8 = std::ptr::null_mut();

            let status = (self.xlib.XGetWindowProperty)(
                self.display,
                window,
                self.net_wm_state,
                0,
                32,
                0,
                XA_ATOM,
                &mut actual_type,
                &mut actual_format,
                &mut count,
                &mut remaining,
                &mut data,
            );

            if status != 0 || data.is_null() || count == 0 {
                if !data.is_null() {
                    (self.xlib.XFree)(data as *mut c_void);
                }
                return false;
            }

            let atoms = std::slice::from_raw_parts(data as *const xlib::Atom, count as usize);
            let result = atoms.contains(&wanted);
            (self.xlib.XFree)(data as *mut c_void);
            result
        }
    }

    fn cleanup(&mut self) {
        unsafe {
            if !self.display.is_null() {
                (self.xlib.XCloseDisplay)(self.display);
                self.display = std::ptr::null_mut();
            }
        }
    }
}

fn run_record_thread() {
    init_x11_threads();

    let xrec = match xrecord::Xf86vmode::open() {
        Ok(x) => x,
        Err(err) => {
            eprintln!("x11-record: libXtst: {err}");
            return;
        }
    };

    let _ = RECORD_FREE_DATA.set(xrec.XRecordFreeData);

    let xlib = match xlib::Xlib::open() {
        Ok(x) => x,
        Err(_) => return,
    };

    unsafe {
        let display = (xlib.XOpenDisplay)(std::ptr::null());
        if display.is_null() {
            return;
        }

        let mut version: c_int = 0;
        let mut revision: c_int = 0;

        if (xrec.XRecordQueryVersion)(display, &mut version, &mut revision) == 0 {
            (xlib.XCloseDisplay)(display);
            return;
        }

        let range = (xrec.XRecordAllocRange)();
        if range.is_null() {
            (xlib.XCloseDisplay)(display);
            return;
        }

        (*range).device_events.first = xlib::KeyPress as u8;
        (*range).device_events.last = xlib::ButtonRelease as u8;

        let mut ranges = [range];
        let mut clients: [xlib::Window; 0] = [];

        let context = (xrec.XRecordCreateContext)(
            display,
            0,
            clients.as_mut_ptr(),
            0,
            ranges.as_mut_ptr(),
            1,
        );

        (xlib.XFree)(range as *mut c_void);

        if context == 0 {
            (xlib.XCloseDisplay)(display);
            return;
        }

        (xrec.XRecordEnableContext)(display, context, Some(record_callback), std::ptr::null_mut());

        (xrec.XRecordFreeContext)(display, context);
        (xlib.XCloseDisplay)(display);
    }
}

unsafe extern "C" fn record_callback(_closure: *mut c_char, data: *mut xrecord::XRecordInterceptData) {
    unsafe {
        if data.is_null() {
            return;
        }

        let intercept = &*data;

        if RECORD_ENABLED.load(Ordering::Relaxed)
            && intercept.category == xrecord::XRecordFromServer
            && !intercept.data.is_null()
            && intercept.data_len >= 32
        {
            let shared_opt = RECORD_SHARED.lock().ok().and_then(|g| g.as_ref().cloned());

            if let Some(shared) = shared_opt {
                let bytes = std::slice::from_raw_parts(intercept.data as *const u8, 32);
                let event_type = bytes[0] as i32;
                let detail = bytes[1] as u32;

                match event_type {
                    xlib::KeyPress => {
                        shared.push_input(InputEvent {
                            kind: KIND_KEY_DOWN,
                            code: detail,
                            ..Default::default()
                        });
                    }
                    xlib::KeyRelease => {
                        shared.push_input(InputEvent {
                            kind: KIND_KEY_UP,
                            code: detail,
                            ..Default::default()
                        });
                    }
                    xlib::ButtonPress => {
                        shared.push_input(InputEvent {
                            kind: KIND_BUTTON_DOWN,
                            code: detail,
                            ..Default::default()
                        });
                    }
                    xlib::ButtonRelease => {
                        shared.push_input(InputEvent {
                            kind: KIND_BUTTON_UP,
                            code: detail,
                            ..Default::default()
                        });
                    }
                    _ => {}
                }
            }
        }

        if let Some(free) = RECORD_FREE_DATA.get() {
            (*free)(data);
        }
    }
}

unsafe fn intern(
    xlib: &xlib::Xlib,
    display: *mut xlib::Display,
    name: &[u8],
) -> Result<xlib::Atom, String> {
    let atom = unsafe { (xlib.XInternAtom)(display, name.as_ptr() as *const c_char, 0) };

    if atom == 0 {
        Err(format!(
            "XInternAtom failed for {}",
            unsafe { CStr::from_bytes_with_nul_unchecked(name).to_string_lossy() }
        ))
    } else {
        Ok(atom)
    }
}

fn translate_modifiers(modifiers: u32) -> u32 {
    let mut mask = 0u32;

    if modifiers & XI_MOD_SHIFT != 0 {
        mask |= xlib::ShiftMask;
    }
    if modifiers & XI_MOD_CONTROL != 0 {
        mask |= xlib::ControlMask;
    }
    if modifiers & XI_MOD_MOD1 != 0 {
        mask |= xlib::Mod1Mask;
    }
    if modifiers & XI_MOD_MOD4 != 0 {
        mask |= xlib::Mod4Mask;
    }

    mask
}