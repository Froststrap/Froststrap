// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use std::path::PathBuf;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, AtomicU32, Ordering};
use std::sync::mpsc::{self, Receiver, Sender};
use std::thread::{self, JoinHandle};
use std::time::Duration;

use crate::decode::{DecodeError, TrackDecoder};
use crate::emitter::{self, Emitter, EmitterError, OutputConfig};

const FALLBACK_RATE: u32 = 48_000;

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

/// Plays one file on its own thread. Dropping it stops playback.
pub struct Player {
    stop: Arc<AtomicBool>,
    volume: Arc<AtomicU32>,
    thread: Option<JoinHandle<()>>,
    events: Receiver<PlayerEvent>,
}

impl Player {
    /// Returns once the file is open and the output has started (or failed to), so callers get
    /// errors directly. With `repeat` the track loops until the player is dropped.
    pub fn play(path: PathBuf, mut output: OutputConfig, volume: f32, repeat: bool) -> Result<Self, PlayError> {
        let decoder = TrackDecoder::open(&path).map_err(PlayError::Decode)?;
        output.sample_rate = decoder.sample_rate.unwrap_or(FALLBACK_RATE);

        let stop = Arc::new(AtomicBool::new(false));
        let volume = Arc::new(AtomicU32::new(volume.clamp(0.0, 1.0).to_bits()));
        let (events_tx, events) = mpsc::channel();
        let (ready_tx, ready_rx) = mpsc::channel();

        let thread = {
            let stop = stop.clone();
            let volume = volume.clone();
            thread::spawn(move || {
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
                run(source, emitter, output, &stop, &volume, &events_tx);
            })
        };

        match ready_rx.recv_timeout(Duration::from_secs(5)) {
            Ok(Ok(())) => Ok(Self { stop, volume, thread: Some(thread), events }),
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

    pub fn events(&self) -> &Receiver<PlayerEvent> {
        &self.events
    }
}

impl Drop for Player {
    fn drop(&mut self) {
        self.stop.store(true, Ordering::Relaxed);
        if let Some(thread) = self.thread.take() {
            let _ = thread.join();
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
    volume: &AtomicU32,
    events: &Sender<PlayerEvent>,
) {
    let mut chunk = Vec::new();

    let result = loop {
        if stop.load(Ordering::Relaxed) {
            let _ = emitter.flush();
            return;
        }

        let rate = match source.decoder.next_stereo(&mut chunk) {
            Ok(Some(rate)) => rate,
            Ok(None) if source.repeat => match TrackDecoder::open(&source.path) {
                Ok(d) => {
                    source.decoder = d;
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
    };

    let event = match result.and_then(|()| emitter.drain().map_err(|e| e.to_string())) {
        Ok(()) => PlayerEvent::Finished,
        Err(e) => PlayerEvent::Failed(e),
    };

    let _ = events.send(event);
}
