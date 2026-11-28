// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

//! Manual test for the audio engine.
//!
//!   cargo run --example play -- --list
//!   cargo run --example play -- <file> [--device <id>] [--pair <n>] [--volume <0-1>] [--info]
//!
//! `--pair 1` = channels 1/2, `--pair 3` = channels 3/4.

use audioplayer::emitter::{self, OutputConfig};
use audioplayer::player::{Player, PlayerEvent};
use std::time::{Duration, Instant};

fn main() {
    let args: Vec<String> = std::env::args().skip(1).collect();
    let flag = |name: &str| args.iter().position(|a| a == name).and_then(|i| args.get(i + 1)).cloned();

    if args.iter().any(|a| a == "--list") {
        match emitter::devices() {
            Ok(devices) => {
                for d in devices {
                    let mark = if d.is_default { " (default)" } else { "" };
                    println!("{}{mark}\n  channels: {}\n  id: {}", d.name, d.channels, d.id);
                }
            }
            Err(e) => println!("listing devices failed: {e}"),
        }
        return;
    }

    let Some(file) = args.first().filter(|a| !a.starts_with("--")).cloned() else {
        println!("usage: play --list | play <file> [--device <id>] [--pair <n>] [--volume <0-1>] [--info]");
        return;
    };

    if args.iter().any(|a| a == "--info") {
        match audioplayer::decode::read_info(file.as_ref()) {
            Ok(i) => println!(
                "title: {:?}\nartist: {:?}\nalbum: {:?}\nduration: {:?}s\nsample rate: {:?}\nchannels: {:?}\ncover: {}",
                i.title,
                i.artist,
                i.album,
                i.duration_secs.map(|d| d.round()),
                i.sample_rate,
                i.channels,
                i.art.map(|a| format!("{} bytes ({})", a.data.len(), a.media_type.unwrap_or_default())).unwrap_or("none".into())
            ),
            Err(e) => println!("reading info failed: {e}"),
        }
    }

    let pair: u16 = flag("--pair").and_then(|p| p.parse().ok()).unwrap_or(1);
    let volume: f32 = flag("--volume").and_then(|v| v.parse().ok()).unwrap_or(0.5);
    let output = OutputConfig { device: flag("--device"), pair: pair.saturating_sub(1), ..Default::default() };

    let started = Instant::now();
    let player = match Player::play(file.into(), output, volume, false, None) {
        Ok(p) => p,
        Err(e) => {
            println!("play failed: {e}");
            return;
        }
    };

    println!("playing on channels {}/{} at {:.0}% volume...", pair, pair + 1, volume * 100.0);
    match player.events().recv_timeout(Duration::from_secs(60 * 15)) {
        Ok(PlayerEvent::Finished) => println!("finished after {:.1}s", started.elapsed().as_secs_f64()),
        Ok(PlayerEvent::Failed(e)) => println!("failed: {e}"),
        Err(_) => println!("timed out"),
    }
}
