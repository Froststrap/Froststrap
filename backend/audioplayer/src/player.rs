// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use std::path::PathBuf;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, AtomicU32, Ordering};
use std::sync::mpsc::{self, Receiver};
use std::thread::{self, JoinHandle};
use std::time::Duration;

const PAUSE_POLL: Duration = Duration::from_millis(20);

use crate::decode::{DecodeError, TrackDecoder};
use crate::emitter::{self, Emitter, EmitterError, OutputConfig};

const FALLBACK_RATE: u32 = 48_000;
const STOP_TIMEOUT: Duration = Duration::from_secs(3);

#[derive(Debug, PartialEq)]
pub enum PlayerEvent {
    Finished,
    Failed(String),
}

#[derive(Debug)]
pub enum PlayError {
    Decode(DecodeError),
    Output(EmitterError),
}

impl std::fmt::Display for PlayError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::Decode(e) => e.fmt(f),
            Self::Output(e) => e.fmt(f),
        }
    }
}

/// Called on the playback thread when a track ends on its own (not when stopped). Keep it short.
pub type EndHandler = Box<dyn FnOnce(&PlayerEvent) + Send>;

/// Plays one file on its own thread. Dropping it stops playback.
pub struct Player {
    stop: Arc<AtomicBool>,
    paused: Arc<AtomicBool>,
    volume: Arc<AtomicU32>,
    thread: Option<JoinHandle<()>>,
    /// Disconnects when the playback thread exits.
    exited: Receiver<()>,
    events: Receiver<PlayerEvent>,
}

impl Player {
    /// Returns once the file is open and the output has started (or failed to), so callers get
    /// errors directly. With `repeat` the track loops until the player is dropped.
    pub fn play(
        path: PathBuf,
        mut output: OutputConfig,
        volume: f32,
        repeat: bool,
        on_end: Option<EndHandler>,
    ) -> Result<Self, PlayError> {
        let decoder = TrackDecoder::open(&path).map_err(PlayError::Decode)?;
        output.sample_rate = decoder.sample_rate.unwrap_or(FALLBACK_RATE);

        let stop = Arc::new(AtomicBool::new(false));
        let paused = Arc::new(AtomicBool::new(false));
        let volume = Arc::new(AtomicU32::new(volume.clamp(0.0, 1.0).to_bits()));
        let (events_tx, events) = mpsc::channel();
        let (ready_tx, ready_rx) = mpsc::channel();
        let (exited_tx, exited) = mpsc::channel::<()>();

        let thread = {
            let stop = stop.clone();
            let paused = paused.clone();
            let volume = volume.clone();
            thread::spawn(move || {
                let _exited = exited_tx;

                let emitter = match emitter::open(&output) {
                    Ok(e) => {
                        let _ = ready_tx.send(Ok(()));
                        e
                    }
                    Err(e) => {
                        let _ = ready_tx.send(Err(e));
                        return;
                    }
                };

                let source = Source { path, repeat, decoder };
                if let Some(event) = run(source, emitter, output, &stop, &paused, &volume) {
                    if let Some(on_end) = on_end {
                        on_end(&event);
                    }
                    let _ = events_tx.send(event);
                }
            })
        };

        match ready_rx.recv_timeout(Duration::from_secs(5)) {
            Ok(Ok(())) => Ok(Self { stop, paused, volume, thread: Some(thread), exited, events }),
            Ok(Err(e)) => Err(PlayError::Output(e)),
            Err(_) => {
                stop.store(true, Ordering::Relaxed);
                Err(PlayError::Output(EmitterError::Device("timed out opening the audio device".into())))
            }
        }
    }

    pub fn set_volume(&self, volume: f32) {
        self.volume.store(volume.clamp(0.0, 1.0).to_bits(), Ordering::Relaxed);
    }

    pub fn set_paused(&self, paused: bool) {
        self.paused.store(paused, Ordering::Relaxed);
    }

    pub fn events(&self) -> &Receiver<PlayerEvent> {
        &self.events
    }
}

impl Drop for Player {
    fn drop(&mut self) {
        self.stop.store(true, Ordering::Relaxed);

        // never let a stuck audio device hang whoever is stopping playback
        match self.exited.recv_timeout(STOP_TIMEOUT) {
            Err(mpsc::RecvTimeoutError::Timeout) => {
                log::warn!("audio thread didn't stop within {STOP_TIMEOUT:?}, leaving it behind");
            }
            _ => {
                if let Some(thread) = self.thread.take() {
                    let _ = thread.join();
                }
            }
        }
    }
}

struct Source {
    path: PathBuf,
    repeat: bool,
    decoder: TrackDecoder,
}

fn run(
    mut source: Source,
    mut emitter: Box<dyn Emitter>,
    mut output: OutputConfig,
    stop: &AtomicBool,
    paused: &AtomicBool,
    volume: &AtomicU32,
) -> Option<PlayerEvent> {
    let mut chunk = Vec::new();
    let mut produced_audio = false;
    let mut holding = false;

    let result = loop {
        if stop.load(Ordering::Relaxed) {
            let _ = emitter.flush();
            return None;
        }

        if paused.load(Ordering::Relaxed) {
            if !holding {
                if let Err(e) = emitter.pause() {
                    break Err(e.to_string());
                }
                holding = true;
            }
            thread::sleep(PAUSE_POLL);
            continue;
        }
        holding = false;

        let rate = match source.decoder.next_stereo(&mut chunk) {
            Ok(Some(rate)) => rate,
            // reopening a file with nothing playable in it would spin forever
            Ok(None) if !produced_audio => break Err("the file contains no playable audio".into()),
            Ok(None) if source.repeat => match TrackDecoder::open(&source.path) {
                Ok(d) => {
                    source.decoder = d;
                    produced_audio = false;
                    continue;
                }
                Err(e) => break Err(e.to_string()),
            },
            Ok(None) => break Ok(()),
            Err(e) => break Err(e.to_string()),
        };

        // headers can be wrong or missing; reopen at the real rate
        if rate != output.sample_rate {
            output.sample_rate = rate;
            emitter = match emitter::open(&output) {
                Ok(e) => e,
                Err(e) => break Err(e.to_string()),
            };
        }

        let gain = f32::from_bits(volume.load(Ordering::Relaxed));
        if gain != 1.0 {
            chunk.iter_mut().for_each(|s| *s *= gain);
        }

        if let Err(e) = emitter.write(&chunk) {
            break Err(e.to_string());
        }
        produced_audio = true;
    };

    match result.and_then(|()| emitter.drain().map_err(|e| e.to_string())) {
        Ok(()) => Some(PlayerEvent::Finished),
        Err(e) => Some(PlayerEvent::Failed(e)),
    }
}
