// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use super::{DeviceInfo, Emitter, EmitterError, OutputConfig, check_pair, spread};
use std::ffi::c_void;

use windows::Win32::Foundation::{CloseHandle, HANDLE, PROPERTYKEY, RPC_E_CHANGED_MODE};
use windows::Win32::Media::Audio::{
    AUDCLNT_SHAREMODE_SHARED, AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM, AUDCLNT_STREAMFLAGS_EVENTCALLBACK,
    AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY, DEVICE_STATE_ACTIVE, IAudioClient, IAudioRenderClient, IMMDevice,
    IMMDeviceEnumerator, MMDeviceEnumerator, WAVEFORMATEX, WAVEFORMATEXTENSIBLE, WAVEFORMATEXTENSIBLE_0, eConsole,
    eRender,
};
use windows::Win32::System::Com::StructuredStorage::PropVariantToStringAlloc;
use windows::Win32::System::Com::{
    CLSCTX_ALL, COINIT_MULTITHREADED, CoCreateInstance, CoInitializeEx, CoTaskMemFree, STGM_READ,
};
use windows::Win32::System::Threading::{CreateEventW, WaitForSingleObject};
use windows::core::{GUID, HSTRING, PCWSTR, PWSTR};

const WAVE_FORMAT_EXTENSIBLE: u16 = 0xFFFE;
const KSDATAFORMAT_SUBTYPE_IEEE_FLOAT: GUID = GUID::from_u128(0x00000003_0000_0010_8000_00aa00389b71);
const PKEY_DEVICE_FRIENDLY_NAME: PROPERTYKEY =
    PROPERTYKEY { fmtid: GUID::from_u128(0xa45c254e_df1c_4efd_8020_67d146a850e0), pid: 14 };

/// 200 ms, in 100 ns units
const BUFFER_DURATION: i64 = 2_000_000;

fn device_err(context: &str, e: windows::core::Error) -> EmitterError {
    EmitterError::Device(format!("{context}: {e}"))
}

fn init_com() -> Result<(), EmitterError> {
    let hr = unsafe { CoInitializeEx(None, COINIT_MULTITHREADED) };
    // already initialised as STA on this thread is fine for what we use
    if hr.is_err() && hr != RPC_E_CHANGED_MODE {
        return Err(EmitterError::Unavailable(format!("CoInitializeEx failed: {:?}", hr)));
    }
    Ok(())
}

fn enumerator() -> Result<IMMDeviceEnumerator, EmitterError> {
    init_com()?;
    unsafe { CoCreateInstance(&MMDeviceEnumerator, None, CLSCTX_ALL) }
        .map_err(|e| EmitterError::Unavailable(format!("no audio device enumerator: {e}")))
}

fn take_string(p: PWSTR) -> String {
    if p.is_null() {
        return String::new();
    }
    let s = unsafe { p.to_string() }.unwrap_or_default();
    unsafe { CoTaskMemFree(Some(p.0 as *const c_void)) };
    s
}

fn device_id(device: &IMMDevice) -> String {
    unsafe { device.GetId() }.map(take_string).unwrap_or_default()
}

fn device_name(device: &IMMDevice) -> String {
    let name = unsafe {
        device
            .OpenPropertyStore(STGM_READ)
            .and_then(|store| store.GetValue(&PKEY_DEVICE_FRIENDLY_NAME))
            .and_then(|value| PropVariantToStringAlloc(&value))
    };
    name.map(take_string).unwrap_or_else(|_| "Unknown device".into())
}

struct MixFormat {
    channels: u16,
    channel_mask: u32,
}

fn mix_format(client: &IAudioClient) -> Result<MixFormat, EmitterError> {
    let ptr = unsafe { client.GetMixFormat() }.map_err(|e| device_err("GetMixFormat", e))?;

    // the format structs are packed, so read them unaligned
    let base = unsafe { std::ptr::read_unaligned(ptr) };
    let channel_mask = if base.wFormatTag == WAVE_FORMAT_EXTENSIBLE {
        unsafe { std::ptr::read_unaligned(ptr as *const WAVEFORMATEXTENSIBLE) }.dwChannelMask
    } else {
        0
    };

    unsafe { CoTaskMemFree(Some(ptr as *const c_void)) };
    Ok(MixFormat { channels: base.nChannels, channel_mask })
}

fn find_device(enumerator: &IMMDeviceEnumerator, id: Option<&str>) -> Result<IMMDevice, EmitterError> {
    match id.filter(|i| !i.is_empty()) {
        Some(id) => unsafe { enumerator.GetDevice(&HSTRING::from(id)) }.map_err(|e| device_err("device not found", e)),
        None => unsafe { enumerator.GetDefaultAudioEndpoint(eRender, eConsole) }
            .map_err(|e| device_err("no default output device", e)),
    }
}

pub fn devices() -> Result<Vec<DeviceInfo>, EmitterError> {
    let enumerator = enumerator()?;
    let default_id = find_device(&enumerator, None).map(|d| device_id(&d)).unwrap_or_default();

    let collection = unsafe { enumerator.EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE) }
        .map_err(|e| device_err("EnumAudioEndpoints", e))?;
    let count = unsafe { collection.GetCount() }.map_err(|e| device_err("GetCount", e))?;

    let mut list = Vec::with_capacity(count as usize);
    for i in 0..count {
        let Ok(device) = (unsafe { collection.Item(i) }) else { continue };
        let Ok(client) = (unsafe { device.Activate::<IAudioClient>(CLSCTX_ALL, None) }) else { continue };
        let Ok(mix) = mix_format(&client) else { continue };

        let id = device_id(&device);
        list.push(DeviceInfo {
            is_default: id == default_id,
            name: device_name(&device),
            channels: mix.channels,
            id,
        });
    }

    Ok(list)
}

pub struct WasapiEmitter {
    client: IAudioClient,
    render: IAudioRenderClient,
    event: HANDLE,
    buffer_frames: u32,
    channels: usize,
    pair: usize,
    started: bool,
    scratch: Vec<f32>,
}

// created and used on the playback thread; COM objects are from the MTA
unsafe impl Send for WasapiEmitter {}

impl WasapiEmitter {
    pub fn open(config: &OutputConfig) -> Result<Self, EmitterError> {
        let enumerator = enumerator()?;
        let device = find_device(&enumerator, config.device.as_deref())?;
        let client: IAudioClient =
            unsafe { device.Activate(CLSCTX_ALL, None) }.map_err(|e| device_err("Activate", e))?;

        // match the device's channel layout so the pair maps 1:1 to its channels
        let mix = mix_format(&client)?;
        check_pair(config.pair, mix.channels)?;

        let channels = mix.channels;
        let block_align = channels * 4;
        let format = WAVEFORMATEXTENSIBLE {
            Format: WAVEFORMATEX {
                wFormatTag: WAVE_FORMAT_EXTENSIBLE,
                nChannels: channels,
                nSamplesPerSec: config.sample_rate,
                nAvgBytesPerSec: config.sample_rate * block_align as u32,
                nBlockAlign: block_align,
                wBitsPerSample: 32,
                cbSize: (size_of::<WAVEFORMATEXTENSIBLE>() - size_of::<WAVEFORMATEX>()) as u16,
            },
            Samples: WAVEFORMATEXTENSIBLE_0 { wValidBitsPerSample: 32 },
            dwChannelMask: mix.channel_mask,
            SubFormat: KSDATAFORMAT_SUBTYPE_IEEE_FLOAT,
        };

        // AUTOCONVERTPCM lets the Windows mixer resample the file's rate to the device's
        let flags = AUDCLNT_STREAMFLAGS_EVENTCALLBACK
            | AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM
            | AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY;

        unsafe {
            client.Initialize(
                AUDCLNT_SHAREMODE_SHARED,
                flags,
                BUFFER_DURATION,
                0,
                &format as *const WAVEFORMATEXTENSIBLE as *const WAVEFORMATEX,
                None,
            )
        }
        .map_err(|e| device_err("Initialize", e))?;

        let event = unsafe { CreateEventW(None, false, false, PCWSTR::null()) }
            .map_err(|e| device_err("CreateEvent", e))?;

        let setup = (|| unsafe {
            client.SetEventHandle(event)?;
            let buffer_frames = client.GetBufferSize()?;
            let render = client.GetService::<IAudioRenderClient>()?;
            Ok::<_, windows::core::Error>((buffer_frames, render))
        })();

        let (buffer_frames, render) = match setup {
            Ok(v) => v,
            Err(e) => {
                let _ = unsafe { CloseHandle(event) };
                return Err(device_err("stream setup", e));
            }
        };

        Ok(Self {
            client,
            render,
            event,
            buffer_frames,
            channels: channels as usize,
            pair: config.pair as usize,
            started: false,
            scratch: Vec::new(),
        })
    }

    fn start(&mut self) -> Result<(), EmitterError> {
        if !self.started {
            unsafe { self.client.Start() }.map_err(|e| device_err("Start", e))?;
            self.started = true;
        }
        Ok(())
    }

    fn padding(&self) -> Result<u32, EmitterError> {
        unsafe { self.client.GetCurrentPadding() }.map_err(|e| device_err("GetCurrentPadding", e))
    }
}

impl Emitter for WasapiEmitter {
    fn write(&mut self, stereo: &[f32]) -> Result<(), EmitterError> {
        let mut out = std::mem::take(&mut self.scratch);
        spread(stereo, self.channels, self.pair, &mut out);

        let total = out.len() / self.channels;
        let mut done = 0;
        let result = loop {
            if done >= total {
                break Ok(());
            }

            let free = match self.padding() {
                Ok(p) => self.buffer_frames.saturating_sub(p) as usize,
                Err(e) => break Err(e),
            };

            if free == 0 {
                // buffer is full: make sure it's playing, then wait for room
                if let Err(e) = self.start() {
                    break Err(e);
                }
                unsafe { WaitForSingleObject(self.event, 500) };
                continue;
            }

            let frames = free.min(total - done);
            let bytes = frames * self.channels * 4;
            let src = &out[done * self.channels..(done + frames) * self.channels];

            let copied = unsafe {
                self.render.GetBuffer(frames as u32).and_then(|dst| {
                    std::ptr::copy_nonoverlapping(src.as_ptr() as *const u8, dst, bytes);
                    self.render.ReleaseBuffer(frames as u32, 0)
                })
            };

            if let Err(e) = copied {
                break Err(device_err("render buffer", e));
            }

            done += frames;
        };

        self.scratch = out;
        result?;
        self.start()
    }

    fn drain(&mut self) -> Result<(), EmitterError> {
        self.start()?;
        while self.padding()? > 0 {
            unsafe { WaitForSingleObject(self.event, 100) };
        }
        unsafe { self.client.Stop() }.map_err(|e| device_err("Stop", e))?;
        self.started = false;
        Ok(())
    }

    fn flush(&mut self) -> Result<(), EmitterError> {
        unsafe {
            let _ = self.client.Stop();
            self.client.Reset().map_err(|e| device_err("Reset", e))?;
        }
        self.started = false;
        Ok(())
    }
}

impl Drop for WasapiEmitter {
    fn drop(&mut self) {
        unsafe {
            let _ = self.client.Stop();
            let _ = CloseHandle(self.event);
        }
    }
}
