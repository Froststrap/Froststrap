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
use player::{PlayError, Player};

const OK: i32 = 0;
const ERR_INVALID_ARG: i32 = 1;
const ERR_OPEN_FILE: i32 = 2;
const ERR_DECODE: i32 = 3;
const ERR_NO_DEVICE: i32 = 4;

static PLAYER: Mutex<Option<Player>> = Mutex::new(None);

// symphonia logs at debug level every time the track loops
#[unsafe(no_mangle)]
pub extern "C" fn set_logger(callback: logger::LoggerCallback) {
    logger::set(callback);
    log::set_max_level(log::LevelFilter::Info);
}

/// Plays `path` on a loop on the default device (channels 1/2), replacing whatever is playing.
///
/// # Safety
/// `path` must be null or a valid nul-terminated UTF-8 string.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn play(path: *const c_char, volume: f32) -> i32 {
    if path.is_null() {
        return ERR_INVALID_ARG;
    }

    let path = unsafe { CStr::from_ptr(path) }.to_string_lossy().into_owned();
    let mut current = PLAYER.lock().unwrap_or_else(|e| e.into_inner());

    // stop the old track first so two streams never overlap
    current.take();

    match Player::play(path.clone().into(), OutputConfig::default(), volume, true) {
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

/// Stops playback and releases the audio device.
#[unsafe(no_mangle)]
pub extern "C" fn stop() {
    PLAYER.lock().unwrap_or_else(|e| e.into_inner()).take();
}
