// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use std::fmt;
use std::fs::File;
use std::path::Path;

use symphonia::core::codecs::audio::{AudioDecoder, AudioDecoderOptions};
use symphonia::core::errors::Error;
use symphonia::core::formats::probe::Hint;
use symphonia::core::formats::{FormatOptions, FormatReader, TrackType};
use symphonia::core::io::MediaSourceStream;
use symphonia::core::meta::{MetadataContainer, MetadataOptions, StandardTag, StandardVisualKey};

#[derive(Debug)]
pub enum DecodeError {
    /// The file couldn't be opened or read.
    Io(String),
    /// The file was read but isn't a supported or valid audio file.
    Unsupported(String),
}

impl fmt::Display for DecodeError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Io(m) | Self::Unsupported(m) => f.write_str(m),
        }
    }
}

impl From<Error> for DecodeError {
    fn from(e: Error) -> Self {
        match e {
            Error::IoError(io) => Self::Io(io.to_string()),
            other => Self::Unsupported(other.to_string()),
        }
    }
}

impl From<std::io::Error> for DecodeError {
    fn from(e: std::io::Error) -> Self {
        Self::Io(e.to_string())
    }
}

#[derive(Debug, Default)]
pub struct TrackInfo {
    pub title: Option<String>,
    pub artist: Option<String>,
    pub album: Option<String>,
    pub duration_secs: Option<f64>,
    pub sample_rate: Option<u32>,
    pub channels: Option<usize>,
    pub art: Option<Art>,
}

#[derive(Debug)]
pub struct Art {
    pub media_type: Option<String>,
    pub data: Vec<u8>,
}

fn open_format(path: &Path) -> Result<Box<dyn FormatReader>, DecodeError> {
    let file = File::open(path)?;
    let mss = MediaSourceStream::new(Box::new(file), Default::default());

    let mut hint = Hint::new();
    if let Some(ext) = path.extension().and_then(|e| e.to_str()) {
        hint.with_extension(ext);
    }

    Ok(symphonia::default::get_probe().probe(
        &hint,
        mss,
        FormatOptions::default(),
        MetadataOptions::default(),
    )?)
}

fn collect(container: &MetadataContainer, info: &mut TrackInfo, art: &mut Vec<(bool, Art)>) {
    for tag in &container.tags {
        let (slot, value) = match &tag.std {
            Some(StandardTag::TrackTitle(v)) => (&mut info.title, v),
            Some(StandardTag::Artist(v)) => (&mut info.artist, v),
            Some(StandardTag::Album(v)) => (&mut info.album, v),
            _ => continue,
        };

        if slot.is_none() && !value.trim().is_empty() {
            *slot = Some(value.trim().to_string());
        }
    }

    for visual in &container.visuals {
        art.push((
            visual.usage == Some(StandardVisualKey::FrontCover),
            Art { media_type: visual.media_type.clone(), data: visual.data.to_vec() },
        ));
    }
}

/// Reads tags, duration and the front cover (or the first picture) without decoding audio.
pub fn read_info(path: &Path) -> Result<TrackInfo, DecodeError> {
    let mut format = open_format(path)?;
    let mut info = TrackInfo::default();
    let mut pictures = Vec::new();

    {
        let mut metadata = format.metadata();
        loop {
            if let Some(revision) = metadata.current() {
                collect(&revision.media, &mut info, &mut pictures);
                for track in &revision.per_track {
                    collect(&track.metadata, &mut info, &mut pictures);
                }
            }
            if metadata.is_latest() {
                break;
            }
            metadata.pop();
        }
    }

    if let Some(track) = format.default_track(TrackType::Audio) {
        let audio = track.codec_params.as_ref().and_then(|p| p.audio());
        info.sample_rate = audio.and_then(|a| a.sample_rate);
        info.channels = audio.and_then(|a| a.channels.as_ref()).map(|c| c.count());

        info.duration_secs = match (track.num_frames, track.time_base, info.sample_rate) {
            (Some(frames), Some(tb), _) => {
                Some(frames as f64 * tb.numer.get() as f64 / tb.denom.get() as f64)
            }
            (Some(frames), None, Some(rate)) => Some(frames as f64 / rate as f64),
            _ => None,
        };
    }

    let front = pictures.iter().position(|(is_front, _)| *is_front);
    info.art = match front {
        Some(i) => Some(pictures.swap_remove(i).1),
        None => pictures.into_iter().next().map(|(_, art)| art),
    };

    Ok(info)
}

/// Decodes a file into interleaved stereo f32 chunks.
pub struct TrackDecoder {
    /// From the file's headers; the real rate of each chunk is returned by `next_stereo`.
    pub sample_rate: Option<u32>,
    format: Box<dyn FormatReader>,
    decoder: Box<dyn AudioDecoder>,
    track_id: u32,
    samples: Vec<f32>,
}

impl TrackDecoder {
    pub fn open(path: &Path) -> Result<Self, DecodeError> {
        let format = open_format(path)?;
        let track = format
            .default_track(TrackType::Audio)
            .ok_or_else(|| DecodeError::Unsupported("no audio track".into()))?;

        let params = track
            .codec_params
            .as_ref()
            .and_then(|p| p.audio())
            .ok_or_else(|| DecodeError::Unsupported("no audio codec parameters".into()))?;

        let sample_rate = params.sample_rate;
        let decoder = symphonia::default::get_codecs()
            .make_audio_decoder(params, &AudioDecoderOptions::default())?;

        Ok(Self { sample_rate, track_id: track.id, format, decoder, samples: Vec::new() })
    }

    /// Fills `out` with the next chunk and returns its sample rate, or `None` at the end.
    pub fn next_stereo(&mut self, out: &mut Vec<f32>) -> Result<Option<u32>, DecodeError> {
        loop {
            let Some(packet) = self.format.next_packet()? else {
                return Ok(None);
            };

            if packet.track_id != self.track_id {
                continue;
            }

            let buffer = match self.decoder.decode(&packet) {
                Ok(buffer) => buffer,
                Err(Error::DecodeError(_)) => continue, // one bad frame shouldn't end the track
                Err(e) => return Err(e.into()),
            };

            // e.g. Vorbis' first packet decodes to nothing
            if buffer.frames() == 0 {
                continue;
            }

            let rate = buffer.spec().rate();
            let channels = buffer.spec().channels().count();
            buffer.copy_to_vec_interleaved(&mut self.samples);

            out.clear();
            match channels {
                0 => continue,
                1 => out.extend(self.samples.iter().flat_map(|&s| [s, s])),
                2 => out.extend_from_slice(&self.samples),
                n => out.extend(self.samples.chunks_exact(n).flat_map(|f| [f[0], f[1]])),
            }

            return Ok(Some(rate));
        }
    }
}
