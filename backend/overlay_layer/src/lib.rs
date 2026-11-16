// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

#[cfg(target_os = "linux")]
pub mod ffi;

#[cfg(target_os = "linux")]
pub mod input;

#[cfg(target_os = "linux")]
pub mod portal;

#[cfg(target_os = "linux")]
pub mod wayland;