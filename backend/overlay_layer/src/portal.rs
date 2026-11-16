// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

use std::collections::{HashMap, VecDeque};
use std::error::Error;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex, MutexGuard};
use std::thread::{self, JoinHandle};

use ashpd::desktop::global_shortcuts::{BindShortcutsOptions, GlobalShortcuts, NewShortcut};
use ashpd::desktop::{CreateSessionOptions, Session};
use futures::channel::mpsc::{UnboundedReceiver, UnboundedSender, unbounded};
use futures::stream::StreamExt;

fn lock<T>(m: &Mutex<T>) -> MutexGuard<'_, T> {
    m.lock().unwrap_or_else(|poisoned| poisoned.into_inner())
}

enum Command {
    Register {
        id: i32,
        shortcut_id: String,
        description: String,
        trigger: String,
    },
    Unregister(i32),
    Shutdown,
}

pub struct Portal {
    sender: UnboundedSender<Command>,
    activations: Arc<Mutex<VecDeque<i32>>>,
    thread: Option<JoinHandle<()>>,
    stop: Arc<AtomicBool>,
}

impl Portal {
    pub fn start() -> Option<Portal> {
        let prepared = async_io::block_on(async {
            let proxy = GlobalShortcuts::new().await.ok()?;
            let session = proxy.create_session(CreateSessionOptions::default()).await.ok()?;
            Some((proxy, session))
        })?;

        let (proxy, session) = prepared;
        let (sender, receiver) = unbounded();
        let activations = Arc::new(Mutex::new(VecDeque::new()));
        let stop = Arc::new(AtomicBool::new(false));

        let thread_activations = activations.clone();
        let thread_stop = stop.clone();

        let thread = thread::Builder::new()
            .name("froststrap-portal".into())
            .spawn(move || {
                if let Err(err) =
                    async_io::block_on(run(proxy, session, receiver, thread_activations, thread_stop))
                {
                    eprintln!("portal stopped: {err}");
                }
            })
            .ok()?;

        Some(Portal {
            sender,
            activations,
            thread: Some(thread),
            stop,
        })
    }

    pub fn register(
        &self,
        id: i32,
        shortcut_id: String,
        description: String,
        trigger: String,
    ) -> bool {
        self.sender
            .unbounded_send(Command::Register {
                id,
                shortcut_id,
                description,
                trigger,
            })
            .is_ok()
    }

    pub fn unregister(&self, id: i32) -> bool {
        self.sender.unbounded_send(Command::Unregister(id)).is_ok()
    }

    pub fn poll(&self) -> i32 {
        lock(&self.activations).pop_front().unwrap_or(-1)
    }

    pub fn shutdown(mut self) {
        self.stop.store(true, Ordering::Relaxed);
        let _ = self.sender.unbounded_send(Command::Shutdown);
        if let Some(handle) = self.thread.take() {
            let _ = handle.join();
        }
    }
}

async fn run(
    portal: GlobalShortcuts,
    session: Session<GlobalShortcuts>,
    mut receiver: UnboundedReceiver<Command>,
    activations: Arc<Mutex<VecDeque<i32>>>,
    stop: Arc<AtomicBool>,
) -> Result<(), Box<dyn Error + Send + Sync>> {
    let mut activation_stream = portal.receive_activated().await?;
    futures::pin_mut!(activation_stream);

    let mut shortcut_map: HashMap<String, i32> = HashMap::new();

    loop {
        if stop.load(Ordering::Relaxed) {
            break;
        }

        futures::select! {
            activation = activation_stream.next() => {
                match activation {
                    Some(activated) => {
                        if let Some(&id) = shortcut_map.get(activated.shortcut_id()) {
                            let mut queue = lock(&activations);
                            if queue.len() >= 256 {
                                queue.pop_front();
                            }
                            queue.push_back(id);
                        }
                    }
                    None => break,
                }
            }
            cmd = receiver.next() => {
                match cmd {
                    Some(Command::Register { id, shortcut_id, description, trigger }) => {
                        let shortcut = NewShortcut::new(shortcut_id.as_str(), description.as_str())
                            .preferred_trigger(trigger.as_str());

                        match portal
                            .bind_shortcuts(&session, &[shortcut], None, BindShortcutsOptions::default())
                            .await
                        {
                            Ok(request) => match request.response() {
                                Ok(_) => {
                                    shortcut_map.insert(shortcut_id, id);
                                }
                                Err(err) => {
                                    eprintln!("portal bind response failed: {err}");
                                }
                            },
                            Err(err) => {
                                eprintln!("portal bind failed: {err}");
                            }
                        }
                    }
                    Some(Command::Unregister(id)) => {
                        shortcut_map.retain(|_, value| *value != id);
                    }
                    Some(Command::Shutdown) | None => break,
                }
            }
        }
    }

    Ok(())
}