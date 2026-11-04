// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

pub mod decode;
pub mod emitter;
pub mod player;

use std::ffi::{CStr, c_char};
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

/// Stops playback and releases the audio device. No track end callback is sent.
#[unsafe(no_mangle)]
pub extern "C" fn stop() {
    PLAYER.lock().unwrap_or_else(|e| e.into_inner()).take();
}
