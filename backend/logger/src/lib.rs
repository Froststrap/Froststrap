// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use std::ffi::{c_char, c_int, CString};
use std::sync::OnceLock;

/// Levels are mapped to the host logger as:
/// 1 = error, 2 = warn, 3 = info, 4 = debug, 5 = trace.
pub type LoggerCallback =
    extern "C" fn(level: c_int, target: *const c_char, message: *const c_char);

static CALLBACK: OnceLock<LoggerCallback> = OnceLock::new();

struct NativeLogger;

impl log::Log for NativeLogger {
    fn enabled(&self, _metadata: &log::Metadata) -> bool {
        CALLBACK.get().is_some()
    }

    fn log(&self, record: &log::Record) {
        let Some(callback) = CALLBACK.get() else {
            eprintln!("[{}] {}", record.target(), record.args());
            return;
        };

        let level = match record.level() {
            log::Level::Error => 1,
            log::Level::Warn => 2,
            log::Level::Info => 3,
            log::Level::Debug => 4,
            log::Level::Trace => 5,
        };

        let target = CString::new(record.target()).unwrap_or_default();
        let message = CString::new(record.args().to_string().replace('\0', "\\0"))
            .unwrap_or_default();

        callback(level, target.as_ptr(), message.as_ptr());
    }

    fn flush(&self) {}
}

static LOGGER: NativeLogger = NativeLogger;

/// Installs the host callback and enables the `log` facade for dylib.
pub fn set(callback: LoggerCallback) {
    let _ = CALLBACK.set(callback);
    let _ = log::set_logger(&LOGGER);
    log::set_max_level(log::LevelFilter::Trace);
}

/// Generates the `set_logger` symbol expected by the host for dylib.
#[macro_export]
macro_rules! export_set_logger {
    () => {
        #[unsafe(no_mangle)]
        pub extern "C" fn set_logger(callback: $crate::LoggerCallback) {
            $crate::set(callback);
        }
    };
}
