// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use std::ffi::{CStr, c_char};
use std::fs::File;
use std::io::BufReader;
use std::sync::Mutex;
use std::sync::mpsc::{self, Sender};
use std::thread;
use std::time::Duration;

use rodio::{Decoder, DeviceSinkBuilder, Player};

// symphonia logs at debug level every time the track loops
#[unsafe(no_mangle)]
pub extern "C" fn set_logger(callback: logger::LoggerCallback) {
    logger::set(callback);
    log::set_max_level(log::LevelFilter::Info);
}

const OK: i32 = 0;
const ERR_INVALID_ARG: i32 = 1;
const ERR_OPEN_FILE: i32 = 2;
const ERR_DECODE: i32 = 3;
const ERR_NO_DEVICE: i32 = 4;

type Source = rodio::decoder::LoopedDecoder<BufReader<File>>;

enum Command {
    Play(Source, f32),
    Volume(f32),
    Stop,
}

// the output device has to stay on the thread that opened it
static WORKER: Mutex<Option<Sender<Command>>> = Mutex::new(None);

fn spawn_worker() -> Option<Sender<Command>> {
    let (tx, rx) = mpsc::channel::<Command>();
    let (ready_tx, ready_rx) = mpsc::channel::<bool>();

    thread::spawn(move || {
        let mut sink = match DeviceSinkBuilder::open_default_sink() {
            Ok(sink) => sink,
            Err(e) => {
                log::error!("failed to open audio output: {e}");
                let _ = ready_tx.send(false);
                return;
            }
        };
        sink.log_on_drop(false);
        let _ = ready_tx.send(true);

        let mut player: Option<Player> = None;

        while let Ok(command) = rx.recv() {
            match command {
                Command::Play(source, volume) => {
                    let p = Player::connect_new(sink.mixer());
                    p.set_volume(volume);
                    p.append(source);
                    player = Some(p);
                }
                Command::Volume(volume) => {
                    if let Some(p) = &player {
                        p.set_volume(volume);
                    }
                }
                Command::Stop => break,
            }
        }

        drop(player);
        log::debug!("audio output closed");
    });

    match ready_rx.recv_timeout(Duration::from_secs(5)) {
        Ok(true) => Some(tx),
        Ok(false) => None,
        Err(_) => {
            log::error!("timed out opening audio output");
            None
        }
    }
}

fn send(command: Command) -> bool {
    let guard = WORKER.lock().unwrap_or_else(|e| e.into_inner());
    guard.as_ref().is_some_and(|tx| tx.send(command).is_ok())
}

/// # Safety
/// `path` must be null or a valid nul-terminated UTF-8 string.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn play(path: *const c_char, volume: f32) -> i32 {
    if path.is_null() {
        return ERR_INVALID_ARG;
    }

    let path = unsafe { CStr::from_ptr(path) }
        .to_string_lossy()
        .into_owned();

    let file = match File::open(&path) {
        Ok(file) => file,
        Err(e) => {
            log::warn!("failed to open '{path}': {e}");
            return ERR_OPEN_FILE;
        }
    };

    let source = match Decoder::new_looped(BufReader::new(file)) {
        Ok(source) => source,
        Err(e) => {
            log::warn!("failed to decode '{path}': {e}");
            return ERR_DECODE;
        }
    };

    let mut guard = WORKER.lock().unwrap_or_else(|e| e.into_inner());

    if guard.is_none() {
        *guard = spawn_worker();
    }

    let sent = guard.as_ref().is_some_and(|tx| {
        tx.send(Command::Play(source, volume.clamp(0.0, 1.0)))
            .is_ok()
    });

    if !sent {
        *guard = None;
        return ERR_NO_DEVICE;
    }

    log::info!("playing '{path}'");
    OK
}

#[unsafe(no_mangle)]
pub extern "C" fn set_volume(volume: f32) {
    send(Command::Volume(volume.clamp(0.0, 1.0)));
}

#[unsafe(no_mangle)]
pub extern "C" fn stop() {
    let mut guard = WORKER.lock().unwrap_or_else(|e| e.into_inner());
    if let Some(tx) = guard.take() {
        let _ = tx.send(Command::Stop);
    }
}
