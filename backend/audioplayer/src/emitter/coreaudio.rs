// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

// Skeleton only: open() reports Unavailable until the TODOs are filled in.
#![allow(dead_code)]

use super::{DeviceInfo, Emitter, EmitterError, OutputConfig, check_pair, spread};

fn not_yet() -> EmitterError {
    EmitterError::Unavailable("CoreAudio backend isn't implemented yet".into())
}

pub struct CoreAudioEmitter {
    channels: usize,
    pair: usize,
    scratch: Vec<f32>,
    // TODO: AudioQueueRef plus whatever hands frames to its callback (e.g. a ring buffer + condvar)
}

// TODO: drop if the handles stay on the playback thread, like the other backends
unsafe impl Send for CoreAudioEmitter {}

impl CoreAudioEmitter {
    pub fn open(config: &OutputConfig) -> Result<Self, EmitterError> {
        // TODO: real channel count of `config.device` (UID), or the default output
        let channels: u16 = 2;
        check_pair(config.pair, channels)?;

        // TODO: AudioQueueNewOutput at `config.sample_rate`, f32 interleaved, `channels` wide;
        // set kAudioQueueProperty_CurrentDevice when a device is given, then AudioQueueStart
        Err(not_yet())
    }
}

impl Emitter for CoreAudioEmitter {
    fn write(&mut self, stereo: &[f32]) -> Result<(), EmitterError> {
        let mut out = std::mem::take(&mut self.scratch);
        spread(stereo, self.channels, self.pair, &mut out);

        // TODO: queue `out`, blocking while ~200 ms is already buffered (keeps stop/volume snappy)
        self.scratch = out;
        Err(not_yet())
    }

    fn drain(&mut self) -> Result<(), EmitterError> {
        // TODO: wait until the queue has played everything (AudioQueueStop with inImmediate = false)
        Err(not_yet())
    }

    fn flush(&mut self) -> Result<(), EmitterError> {
        // TODO: AudioQueueReset and drop pending frames
        Err(not_yet())
    }

    fn pause(&mut self) -> Result<(), EmitterError> {
        // TODO: AudioQueuePause; the next write should AudioQueueStart again
        Err(not_yet())
    }
}

impl Drop for CoreAudioEmitter {
    fn drop(&mut self) {
        // TODO: AudioQueueDispose
    }
}

pub fn devices() -> Result<Vec<DeviceInfo>, EmitterError> {
    // TODO: kAudioHardwarePropertyDevices, keeping devices with output streams; id = device UID
    Ok(vec![DeviceInfo { id: String::new(), name: "Default".into(), channels: 2, is_default: true }])
}
