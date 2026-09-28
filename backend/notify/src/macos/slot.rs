// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use std::sync::{Arc, Condvar, Mutex};
use std::time::Duration;

pub(super) type Slot<T> = Arc<(Mutex<Option<T>>, Condvar)>;

pub(super) fn new_slot<T>() -> Slot<T> {
    Arc::new((Mutex::new(None), Condvar::new()))
}

pub(super) fn fulfill<T>(slot: &Slot<T>, value: T) {
    let (lock, cvar) = &**slot;
    *lock.lock().unwrap() = Some(value);
    cvar.notify_one();
}

pub(super) fn wait_for<T: Copy>(slot: &Slot<T>, secs: u64) -> Option<T> {
    let (lock, cvar) = &**slot;
    let guard = lock.lock().unwrap();
    let (guard, _) = cvar
        .wait_timeout_while(guard, Duration::from_secs(secs), |r| r.is_none())
        .unwrap();
    *guard
}
