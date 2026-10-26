#[cfg(target_os = "linux")]
pub mod common;

#[cfg(target_os = "linux")]
pub mod ffi;

#[cfg(target_os = "linux")]
pub mod wayland;

#[cfg(target_os = "linux")]
pub mod x11;