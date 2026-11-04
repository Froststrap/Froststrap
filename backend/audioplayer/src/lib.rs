// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

pub mod decode;
pub mod emitter;
pub mod player;

use std::ffi::{CStr, CString, c_char};
use std::path::Path;
use std::ptr;
use std::sync::Mutex;

use decode::DecodeError;
use emitter::OutputConfig;
use player::{PlayError, Player, PlayerEvent};

const OK: i32 = 0;
const ERR_INVALID_ARG: i32 = 1;
const ERR_OPEN_FILE: i32 = 2;
const ERR_DECODE: i32 = 3;
const ERR_NO_DEVICE: i32 = 4;

/// `failed` is 0 when the track played to the end, 1 when playback broke off.
pub type TrackEndCallback = extern "C" fn(id: u64, failed: i32);

static PLAYER: Mutex<Option<Player>> = Mutex::new(None);
static ON_TRACK_END: Mutex<Option<TrackEndCallback>> = Mutex::new(None);

unsafe fn str_arg(s: *const c_char) -> Option<String> {
    (!s.is_null()).then(|| unsafe { CStr::from_ptr(s) }.to_string_lossy().into_owned())
}

// symphonia logs at debug level for every packet it skips
#[unsafe(no_mangle)]
pub extern "C" fn set_logger(callback: logger::LoggerCallback) {
    logger::set(callback);
    log::set_max_level(log::LevelFilter::Info);
}

#[unsafe(no_mangle)]
pub extern "C" fn set_track_end_callback(callback: Option<TrackEndCallback>) {
    *ON_TRACK_END.lock().unwrap_or_else(|e| e.into_inner()) = callback;
}

/// Plays `path` once on the default device (channels 1/2), replacing whatever is playing.
/// When it ends on its own, the track end callback gets `id`.
///
/// # Safety
/// `path` must be null or a valid nul-terminated UTF-8 string.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn play_track(path: *const c_char, volume: f32, id: u64) -> i32 {
    let Some(path) = (unsafe { str_arg(path) }) else {
        return ERR_INVALID_ARG;
    };

    let mut current = PLAYER.lock().unwrap_or_else(|e| e.into_inner());

    // stop the old track first so two streams never overlap
    current.take();

    let name = path.clone();
    let on_end: player::EndHandler = Box::new(move |event| {
        if let PlayerEvent::Failed(e) = event {
            log::warn!("playback of '{name}' stopped: {e}");
        }

        let callback = *ON_TRACK_END.lock().unwrap_or_else(|e| e.into_inner());
        if let Some(callback) = callback {
            callback(id, matches!(event, PlayerEvent::Failed(_)) as i32);
        }
    });

    match Player::play(path.clone().into(), OutputConfig::default(), volume, false, Some(on_end)) {
        Ok(player) => {
            log::info!("playing '{path}'");
            *current = Some(player);
            OK
        }
        Err(e) => {
            log::warn!("failed to play '{path}': {e}");
            match e {
                PlayError::Decode(DecodeError::Io(_)) => ERR_OPEN_FILE,
                PlayError::Decode(DecodeError::Unsupported(_)) => ERR_DECODE,
                PlayError::Output(_) => ERR_NO_DEVICE,
            }
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn set_volume(volume: f32) {
    if let Some(player) = PLAYER.lock().unwrap_or_else(|e| e.into_inner()).as_ref() {
        player.set_volume(volume);
    }
}

/// Holds the current track (non-zero) or carries on (zero). Does nothing if nothing is playing.
#[unsafe(no_mangle)]
pub extern "C" fn set_paused(paused: i32) {
    if let Some(player) = PLAYER.lock().unwrap_or_else(|e| e.into_inner()).as_ref() {
        player.set_paused(paused != 0);
    }
}

/// Stops playback and releases the audio device. No track end callback is sent.
#[unsafe(no_mangle)]
pub extern "C" fn stop() {
    PLAYER.lock().unwrap_or_else(|e| e.into_inner()).take();
}

/// Tags, duration and cover of one file. Strings stay valid until `track_info_free`.
pub struct TrackInfoHandle {
    title: Option<CString>,
    artist: Option<CString>,
    album: Option<CString>,
    duration: f64,
    art: Vec<u8>,
}

fn c_string(s: Option<String>) -> Option<CString> {
    s.and_then(|s| CString::new(s.replace('\0', "")).ok())
}

fn c_ptr(s: &Option<CString>) -> *const c_char {
    s.as_ref().map_or(ptr::null(), |s| s.as_ptr())
}

/// Returns null if the file can't be read.
///
/// # Safety
/// `path` must be null or a valid nul-terminated UTF-8 string.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn track_info_open(path: *const c_char) -> *mut TrackInfoHandle {
    let Some(path) = (unsafe { str_arg(path) }) else {
        return ptr::null_mut();
    };

    match decode::read_info(Path::new(&path)) {
        Ok(info) => Box::into_raw(Box::new(TrackInfoHandle {
            title: c_string(info.title),
            artist: c_string(info.artist),
            album: c_string(info.album),
            duration: info.duration_secs.unwrap_or(-1.0),
            art: info.art.map(|a| a.data).unwrap_or_default(),
        })),
        Err(e) => {
            log::warn!("couldn't read info for '{path}': {e}");
            ptr::null_mut()
        }
    }
}

/// # Safety
/// `info` must come from `track_info_open` and not be freed yet.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn track_info_title(info: *const TrackInfoHandle) -> *const c_char {
    unsafe { info.as_ref() }.map_or(ptr::null(), |i| c_ptr(&i.title))
}

/// # Safety
/// `info` must come from `track_info_open` and not be freed yet.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn track_info_artist(info: *const TrackInfoHandle) -> *const c_char {
    unsafe { info.as_ref() }.map_or(ptr::null(), |i| c_ptr(&i.artist))
}

/// # Safety
/// `info` must come from `track_info_open` and not be freed yet.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn track_info_album(info: *const TrackInfoHandle) -> *const c_char {
    unsafe { info.as_ref() }.map_or(ptr::null(), |i| c_ptr(&i.album))
}

/// Seconds, or a negative value if unknown.
///
/// # Safety
/// `info` must come from `track_info_open` and not be freed yet.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn track_info_duration(info: *const TrackInfoHandle) -> f64 {
    unsafe { info.as_ref() }.map_or(-1.0, |i| i.duration)
}

/// Encoded cover image (usually JPEG or PNG). Sets `len` to 0 and returns null if there's none.
///
/// # Safety
/// `info` must come from `track_info_open` and not be freed yet; `len` must be writable.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn track_info_art(info: *const TrackInfoHandle, len: *mut usize) -> *const u8 {
    let art = unsafe { info.as_ref() }.map(|i| i.art.as_slice()).unwrap_or_default();
    if !len.is_null() {
        unsafe { *len = art.len() };
    }
    if art.is_empty() { ptr::null() } else { art.as_ptr() }
}

/// # Safety
/// `info` must come from `track_info_open` and is invalid afterwards.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn track_info_free(info: *mut TrackInfoHandle) {
    if !info.is_null() {
        drop(unsafe { Box::from_raw(info) });
    }
}
