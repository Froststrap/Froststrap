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

/// Plays `path` once, replacing whatever is playing. `device` is an id from `devices_open`
/// (null or empty for the default) and `pair` the first channel, 0-based (0 = channels 1/2).
/// Falls back to the default output if that device or pair can't be opened.
/// When it ends on its own, the track end callback gets `id`.
///
/// # Safety
/// `path` and `device` must be null or valid nul-terminated UTF-8 strings.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn play_track(path: *const c_char, volume: f32, id: u64, device: *const c_char, pair: u16) -> i32 {
    let Some(path) = (unsafe { str_arg(path) }) else {
        return ERR_INVALID_ARG;
    };
    let output = OutputConfig { device: unsafe { str_arg(device) }.filter(|d| !d.is_empty()), pair, ..Default::default() };

    let mut current = PLAYER.lock().unwrap_or_else(|e| e.into_inner());

    // stop the old track first so two streams never overlap
    current.take();

    let mut result = Player::play(path.clone().into(), output.clone(), volume, false, Some(end_handler(&path, id)));
    if let Err(PlayError::Output(e)) = &result
        && (output.device.is_some() || output.pair != 0)
    {
        log::warn!("couldn't open {output:?} ({e}), using the default output");
        result = Player::play(path.clone().into(), OutputConfig::default(), volume, false, Some(end_handler(&path, id)));
    }

    match result {
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

fn end_handler(path: &str, id: u64) -> player::EndHandler {
    let name = path.to_owned();
    Box::new(move |event| {
        if let PlayerEvent::Failed(e) = event {
            log::warn!("playback of '{name}' stopped: {e}");
        }

        let callback = *ON_TRACK_END.lock().unwrap_or_else(|e| e.into_inner());
        if let Some(callback) = callback {
            callback(id, matches!(event, PlayerEvent::Failed(_)) as i32);
        }
    })
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

/// Output devices. Strings stay valid until `devices_free`.
pub struct DeviceListHandle {
    devices: Vec<(CString, CString, u16, bool)>,
}

/// Returns null if the devices can't be listed.
#[unsafe(no_mangle)]
pub extern "C" fn devices_open() -> *mut DeviceListHandle {
    match emitter::devices() {
        Ok(list) => Box::into_raw(Box::new(DeviceListHandle {
            devices: list
                .into_iter()
                .filter_map(|d| Some((c_string(Some(d.id))?, c_string(Some(d.name))?, d.channels, d.is_default)))
                .collect(),
        })),
        Err(e) => {
            log::warn!("couldn't list output devices: {e}");
            ptr::null_mut()
        }
    }
}

/// # Safety
/// `list` must come from `devices_open` and not be freed yet.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn devices_count(list: *const DeviceListHandle) -> usize {
    unsafe { list.as_ref() }.map_or(0, |l| l.devices.len())
}

/// Id, name, channel count and whether it's the default, of device `index`. False if out of range.
///
/// # Safety
/// `list` must come from `devices_open` and not be freed yet; the out pointers must be writable.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn devices_get(
    list: *const DeviceListHandle,
    index: usize,
    id: *mut *const c_char,
    name: *mut *const c_char,
    channels: *mut u16,
    is_default: *mut i32,
) -> bool {
    let Some((d_id, d_name, d_channels, d_default)) = unsafe { list.as_ref() }.and_then(|l| l.devices.get(index)) else {
        return false;
    };
    if id.is_null() || name.is_null() || channels.is_null() || is_default.is_null() {
        return false;
    }
    unsafe {
        *id = d_id.as_ptr();
        *name = d_name.as_ptr();
        *channels = *d_channels;
        *is_default = *d_default as i32;
    }
    true
}

/// # Safety
/// `list` must come from `devices_open` and is invalid afterwards.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn devices_free(list: *mut DeviceListHandle) {
    if !list.is_null() {
        drop(unsafe { Box::from_raw(list) });
    }
}
