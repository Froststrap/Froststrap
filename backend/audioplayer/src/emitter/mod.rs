// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

#[cfg(target_os = "linux")]
mod pulse;
#[cfg(target_os = "windows")]
mod wasapi;

use std::fmt;

/// Where to play and which stereo pair of the device gets the audio.
#[derive(Clone, Debug, Default)]
pub struct OutputConfig {
    /// Backend device id, `None` for the system default.
    pub device: Option<String>,
    /// First channel of the pair, 0-based (0 = channels 1/2).
    pub pair: u16,
    pub sample_rate: u32,
}

#[derive(Clone, Debug)]
pub struct DeviceInfo {
    pub id: String,
    pub name: String,
    pub channels: u16,
    pub is_default: bool,
}

#[derive(Debug)]
pub enum EmitterError {
    InvalidConfig(String),
    Unavailable(String),
    Device(String),
}

impl fmt::Display for EmitterError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::InvalidConfig(m) => write!(f, "invalid output config: {m}"),
            Self::Unavailable(m) => write!(f, "audio backend unavailable: {m}"),
            Self::Device(m) => write!(f, "audio device error: {m}"),
        }
    }
}

/// An open output stream. Takes interleaved stereo f32 frames at the configured sample rate;
/// the OS mixer converts to the device rate.
pub trait Emitter: Send {
    fn write(&mut self, stereo: &[f32]) -> Result<(), EmitterError>;
    /// Blocks until everything written has played.
    fn drain(&mut self) -> Result<(), EmitterError>;
    /// Drops anything still buffered.
    fn flush(&mut self) -> Result<(), EmitterError>;
}

pub fn open(config: &OutputConfig) -> Result<Box<dyn Emitter>, EmitterError> {
    #[cfg(target_os = "windows")]
    return wasapi::WasapiEmitter::open(config).map(|e| Box::new(e) as Box<dyn Emitter>);

    #[cfg(target_os = "linux")]
    return pulse::PulseEmitter::open(config).map(|e| Box::new(e) as Box<dyn Emitter>);

    #[cfg(not(any(target_os = "windows", target_os = "linux")))]
    {
        _ = config;
        Err(EmitterError::Unavailable("no audio backend for this platform yet".into()))
    }
}

pub fn devices() -> Result<Vec<DeviceInfo>, EmitterError> {
    #[cfg(target_os = "windows")]
    return wasapi::devices();

    #[cfg(target_os = "linux")]
    return pulse::devices();

    #[cfg(not(any(target_os = "windows", target_os = "linux")))]
    Err(EmitterError::Unavailable("no audio backend for this platform yet".into()))
}

pub(crate) fn check_pair(pair: u16, channels: u16) -> Result<(), EmitterError> {
    if channels < 2 || pair + 1 >= channels {
        return Err(EmitterError::InvalidConfig(format!(
            "channels {}/{} don't exist on a {channels}-channel device",
            pair + 1,
            pair + 2
        )));
    }
    Ok(())
}

/// Places a stereo signal on `pair` of a `channels`-wide frame, leaving the rest silent.
pub(crate) fn spread(stereo: &[f32], channels: usize, pair: usize, out: &mut Vec<f32>) {
    let frames = stereo.len() / 2;
    out.clear();
    out.resize(frames * channels, 0.0);

    for i in 0..frames {
        out[i * channels + pair] = stereo[i * 2];
        out[i * channels + pair + 1] = stereo[i * 2 + 1];
    }
}
