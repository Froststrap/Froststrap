use super::{SoberRect, WindowTracker};
use std::sync::mpsc::{self, Receiver};
use std::thread;
use std::time::Duration;

pub struct Fullscreen;

impl WindowTracker for Fullscreen {
    fn query(&self) -> Option<SoberRect> { None }

    fn watch(&self) -> Option<Receiver<()>> {
        let (tx, rx) = mpsc::channel();
        thread::spawn(move || {
            while tx.send(()).is_ok() {
                thread::sleep(Duration::from_millis(500));
            }
        });
        Some(rx)
    }

    fn uses_output(&self) -> bool { true }

    fn focus_known(&self) -> bool { false }
}