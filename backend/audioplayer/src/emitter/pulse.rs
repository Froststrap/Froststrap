// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use super::{DeviceInfo, Emitter, EmitterError, OutputConfig, check_pair, spread};
use std::ffi::{CStr, CString, c_char, c_int, c_void};
use std::ptr;

const PA_SAMPLE_FLOAT32LE: c_int = 5;
const PA_STREAM_PLAYBACK: c_int = 1;
const PA_CHANNEL_MAP_AUX: c_int = 2;
const PA_CHANNEL_MAP_DEFAULT: c_int = 0;
const PA_CHANNELS_MAX: usize = 32;

#[repr(C)]
struct SampleSpec {
    format: c_int,
    rate: u32,
    channels: u8,
}

#[repr(C)]
struct ChannelMap {
    channels: u8,
    map: [c_int; PA_CHANNELS_MAX],
}

#[repr(C)]
struct BufferAttr {
    maxlength: u32,
    tlength: u32,
    prebuf: u32,
    minreq: u32,
    fragsize: u32,
}

#[link(name = "pulse-simple")]
unsafe extern "C" {
    fn pa_simple_new(
        server: *const c_char,
        name: *const c_char,
        dir: c_int,
        dev: *const c_char,
        stream_name: *const c_char,
        ss: *const SampleSpec,
        map: *const ChannelMap,
        attr: *const BufferAttr,
        error: *mut c_int,
    ) -> *mut c_void;
    fn pa_simple_write(s: *mut c_void, data: *const c_void, bytes: usize, error: *mut c_int) -> c_int;
    fn pa_simple_drain(s: *mut c_void, error: *mut c_int) -> c_int;
    fn pa_simple_flush(s: *mut c_void, error: *mut c_int) -> c_int;
    fn pa_simple_free(s: *mut c_void);
}

#[link(name = "pulse")]
unsafe extern "C" {
    fn pa_strerror(error: c_int) -> *const c_char;
    fn pa_channel_map_init_auto(m: *mut ChannelMap, channels: u32, def: c_int) -> *mut ChannelMap;
}

fn error_text(code: c_int) -> String {
    unsafe { CStr::from_ptr(pa_strerror(code)) }.to_string_lossy().into_owned()
}

pub struct PulseEmitter {
    handle: *mut c_void,
    channels: usize,
    pair: usize,
    scratch: Vec<f32>,
}

// pa_simple is only touched from the thread that owns the emitter
unsafe impl Send for PulseEmitter {}

impl PulseEmitter {
    pub fn open(config: &OutputConfig) -> Result<Self, EmitterError> {
        // TODO: read the sink's real channel count/map via the async API; until then the stream
        // is as wide as the requested pair needs
        let channels = (config.pair as usize + 2).max(2);
        if channels > PA_CHANNELS_MAX {
            return Err(EmitterError::InvalidConfig("too many channels".into()));
        }
        check_pair(config.pair, channels as u16)?;

        let spec = SampleSpec { format: PA_SAMPLE_FLOAT32LE, rate: config.sample_rate, channels: channels as u8 };

        // stereo keeps the normal front-left/right map; wider devices are addressed by index
        let def = if channels == 2 { PA_CHANNEL_MAP_DEFAULT } else { PA_CHANNEL_MAP_AUX };
        let mut map = ChannelMap { channels: 0, map: [0; PA_CHANNELS_MAX] };
        if unsafe { pa_channel_map_init_auto(&mut map, channels as u32, def) }.is_null() {
            return Err(EmitterError::InvalidConfig("no channel map for this channel count".into()));
        }

        // ~200 ms of buffer so stop and volume changes feel immediate
        let frame_bytes = (channels * 4) as u32;
        let attr = BufferAttr {
            maxlength: u32::MAX,
            tlength: config.sample_rate / 5 * frame_bytes,
            prebuf: u32::MAX,
            minreq: u32::MAX,
            fragsize: u32::MAX,
        };

        let name = CString::new("Froststrap").unwrap();
        let stream = CString::new("Background music").unwrap();
        let device = config
            .device
            .as_deref()
            .filter(|d| !d.is_empty())
            .map(|d| CString::new(d).unwrap_or_default());

        let mut error = 0;
        let handle = unsafe {
            pa_simple_new(
                ptr::null(),
                name.as_ptr(),
                PA_STREAM_PLAYBACK,
                device.as_ref().map_or(ptr::null(), |d| d.as_ptr()),
                stream.as_ptr(),
                &spec,
                &map,
                &attr,
                &mut error,
            )
        };

        if handle.is_null() {
            return Err(EmitterError::Unavailable(error_text(error)));
        }

        Ok(Self { handle, channels, pair: config.pair as usize, scratch: Vec::new() })
    }

    fn check(&self, result: c_int, error: c_int) -> Result<(), EmitterError> {
        if result < 0 { Err(EmitterError::Device(error_text(error))) } else { Ok(()) }
    }
}

impl Emitter for PulseEmitter {
    fn write(&mut self, stereo: &[f32]) -> Result<(), EmitterError> {
        let mut out = std::mem::take(&mut self.scratch);
        spread(stereo, self.channels, self.pair, &mut out);

        let mut error = 0;
        let result = unsafe {
            pa_simple_write(self.handle, out.as_ptr().cast(), out.len() * 4, &mut error)
        };

        self.scratch = out;
        self.check(result, error)
    }

    fn drain(&mut self) -> Result<(), EmitterError> {
        let mut error = 0;
        let result = unsafe { pa_simple_drain(self.handle, &mut error) };
        self.check(result, error)
    }

    fn flush(&mut self) -> Result<(), EmitterError> {
        let mut error = 0;
        let result = unsafe { pa_simple_flush(self.handle, &mut error) };
        self.check(result, error)
    }

    // pa_simple can't cork, so drop what's buffered to stop right away
    fn pause(&mut self) -> Result<(), EmitterError> {
        self.flush()
    }
}

impl Drop for PulseEmitter {
    fn drop(&mut self) {
        unsafe { pa_simple_free(self.handle) };
    }
}

pub fn devices() -> Result<Vec<DeviceInfo>, EmitterError> {
    // TODO: list sinks with their channel counts once the async API is in
    Ok(vec![DeviceInfo { id: String::new(), name: "Default".into(), channels: 2, is_default: true }])
}
