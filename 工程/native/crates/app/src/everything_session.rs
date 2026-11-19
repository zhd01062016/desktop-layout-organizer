//! The bundled search engine shares the desktop application's lifetime.
use std::os::windows::process::CommandExt;
use std::{path::PathBuf, process::Command, sync::mpsc, thread};

pub struct EverythingSession {
    stop: mpsc::Sender<()>,
    worker: Option<thread::JoinHandle<()>>,
}

impl EverythingSession {
    pub fn start() -> Option<Self> {
        let exe = std::env::current_exe()
            .ok()?
            .parent()?
            .parent()?
            .join("Everything")
            .join("Everything.exe");
        if !exe.is_file() {
            return None;
        }
        let (stop, wait) = mpsc::channel();
        let worker = thread::Builder::new()
            .name("pecofence-search-lifetime".into())
            .spawn(move || {
                let sc = PathBuf::from(
                    std::env::var_os("SystemRoot").unwrap_or_else(|| r"C:\Windows".into()),
                )
                .join("System32")
                .join("sc.exe");
                let service = Command::new(&sc)
                    .args(["start", "Everything (1.5a)"])
                    .creation_flags(0x0800_0000)
                    .output();
                match service {
                    Ok(output)
                        if !output.status.success() && output.status.code() != Some(1056) =>
                    {
                        tracing::warn!("search service start request was rejected");
                    }
                    Err(error) => tracing::warn!(%error, "search service start failed"),
                    _ => {}
                }
                let client = Command::new(&exe)
                    .current_dir(exe.parent().unwrap())
                    .arg("-startup")
                    .creation_flags(0x0800_0000)
                    .spawn();
                if let Err(error) = client {
                    tracing::warn!(%error, "search client start failed");
                }
                let _ = wait.recv();
                let _ = Command::new(&exe)
                    .arg("-exit")
                    .creation_flags(0x0800_0000)
                    .status();
                let _ = Command::new(&sc)
                    .args(["stop", "Everything (1.5a)"])
                    .creation_flags(0x0800_0000)
                    .output();
            })
            .ok()?;
        Some(Self {
            stop,
            worker: Some(worker),
        })
    }
}

impl Drop for EverythingSession {
    fn drop(&mut self) {
        let _ = self.stop.send(());
        if let Some(worker) = self.worker.take() {
            let _ = worker.join();
        }
    }
}
