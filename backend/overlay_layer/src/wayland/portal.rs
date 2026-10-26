use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Mutex;
use std::thread;
use std::time::Duration;

use ashpd::desktop::global_shortcuts::{GlobalShortcuts, NewShortcut};
use ashpd::WindowIdentifier;
use futures_lite::StreamExt;

use crate::common::{KIND_HOTKEY, InputEvent, SharedRef};

#[derive(Clone, Debug)]
pub struct HotkeyRequest {
    pub id: i32,
    pub description: String,
    pub trigger: String,
}

static REQUESTS: Mutex<Vec<HotkeyRequest>> = Mutex::new(Vec::new());
static AVAILABLE: AtomicBool = AtomicBool::new(false);

pub fn available() -> bool {
    AVAILABLE.load(Ordering::SeqCst)
}

pub fn request(id: i32, description: &str, trigger: &str) {
    if let Ok(mut list) = REQUESTS.lock() {
        list.retain(|r| r.id != id);
        list.push(HotkeyRequest {
            id,
            description: description.to_string(),
            trigger: trigger.to_string(),
        });
    }
}

pub fn unregister(id: i32) {
    if let Ok(mut list) = REQUESTS.lock() {
        list.retain(|r| r.id != id);
    }
}

pub fn start(shared: SharedRef) {
    thread::Builder::new()
        .name("portal-shortcuts".into())
        .spawn(move || {
            let runtime = match tokio::runtime::Builder::new_current_thread().enable_all().build() {
                Ok(rt) => rt,
                Err(err) => {
                    eprintln!("portal: runtime: {err}");
                    return;
                }
            };
            runtime.block_on(run(shared));
        })
        .ok();
}

async fn run(shared: SharedRef) {
    let proxy = match GlobalShortcuts::new().await {
        Ok(proxy) => proxy,
        Err(err) => {
            eprintln!("portal: GlobalShortcuts unavailable: {err}");
            AVAILABLE.store(false, Ordering::SeqCst);
            return;
        }
    };

    let session = match proxy.create_session().await {
        Ok(session) => session,
        Err(err) => {
            eprintln!("portal: create_session failed: {err}");
            AVAILABLE.store(false, Ordering::SeqCst);
            return;
        }
    };

    let mut activated = match proxy.receive_activated().await {
        Ok(stream) => stream,
        Err(err) => {
            eprintln!("portal: Activated stream failed: {err}");
            AVAILABLE.store(false, Ordering::SeqCst);
            return;
        }
    };

    loop {
        let pending: Vec<HotkeyRequest> = REQUESTS
            .lock()
            .map(|mut list| std::mem::take(&mut *list))
            .unwrap_or_default();

        if !pending.is_empty() {
            let shortcuts: Vec<NewShortcut> = pending
                .iter()
                .map(|r| {
                    NewShortcut::new(format!("froststrap-{}", r.id), r.description.clone())
                        .preferred_trigger(r.trigger.as_str())
                })
                .collect();

            match proxy
                .bind_shortcuts(&session, &shortcuts, &WindowIdentifier::default())
                .await
                .and_then(|request| request.response())
            {
                Ok(bound) => {
                    AVAILABLE.store(true, Ordering::SeqCst);
                    for shortcut in bound.shortcuts() {
                        eprintln!(
                            "portal: bound {} as {}",
                            shortcut.id(),
                            shortcut.trigger_description()
                        );
                    }
                }
                Err(err) => {
                    AVAILABLE.store(false, Ordering::SeqCst);
                    eprintln!(
                        "portal: BindShortcuts failed ({err}); evdev fallback stays active"
                    );
                }
            }
        }

        tokio::select! {
            biased;
            signal = activated.next() => {
                match signal {
                    Some(signal) => {
                        if let Ok(id) = signal
                            .shortcut_id()
                            .trim_start_matches("froststrap-")
                            .parse::<i32>()
                        {
                            shared.push_input(InputEvent {
                                kind: KIND_HOTKEY,
                                code: id as u32,
                                ..InputEvent::default()
                            });
                        }
                    }
                    None => {
                        eprintln!("portal: Activated stream closed");
                        AVAILABLE.store(false, Ordering::SeqCst);
                        return;
                    }
                }
            }
            _ = tokio::time::sleep(Duration::from_millis(100)) => {}
        }
    }
}