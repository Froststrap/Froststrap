//! helpers to be able to do hot
//! functions quick

use std::ffi::OsStr;
use std::io::{self, Read, Write};
use std::process::{Command, Stdio};
use std::thread;

pub mod log {
    use std::fmt;

    const RESET: &str = "\x1b[0m";
    pub const GRAY: &str = "\x1b[90m";
    pub const BLUE: &str = "\x1b[34m";
    pub const YELLOW: &str = "\x1b[33m";
    pub const RED: &str = "\x1b[31m";

    pub fn timestamp() -> String {
        use std::time::{SystemTime, UNIX_EPOCH};
        let secs = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_secs();
        let (h, m, s) = ((secs / 3600) % 24, (secs / 60) % 60, secs % 60);
        format!("{h:02}:{m:02}:{s:02}")
    }

    pub fn paint(color: &str, text: &str) -> String {
        format!("{color}{text}{RESET}")
    }

    pub fn emit(to_stderr: bool, color: &str, level: &str, args: std::fmt::Arguments<'_>) {
        let line = format!("{} [{}] {}", paint(GRAY, &timestamp()), paint(color, level), args);
        if to_stderr { eprintln!("{line}"); } else { println!("{line}"); }
    }

    pub struct Blue<T>(pub T);

    macro_rules! forward {
        ($($tr:ident),*) => {$(
            impl<T: fmt::$tr> fmt::$tr for Blue<T> {
                fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
                    f.write_str(BLUE)?;
                    fmt::$tr::fmt(&self.0, f)?;
                    f.write_str(RESET)
                }
            }
        )*};
    }
    forward!(Display, Debug, LowerHex, UpperHex, Binary);

    #[macro_export]
    macro_rules! __log {
        ($err:expr, $color:expr, $level:literal, $fmt:literal $(, $arg:expr)* $(,)?) => {
            $crate::utils::log::emit($err, $color, $level, format_args!($fmt $(, $crate::utils::log::Blue(&$arg))*))
        };
    }

    #[macro_export]
    macro_rules! __info {
        ($($t:tt)*) => { $crate::__log!(false, $crate::utils::log::BLUE, "INF", $($t)*) };
    }

    #[macro_export]
    macro_rules! __warn {
        ($($t:tt)*) => { $crate::__log!(true, $crate::utils::log::YELLOW, "WRN", $($t)*) };
    }

    #[macro_export]
    macro_rules! __error {
        ($($t:tt)*) => { $crate::__log!(true, $crate::utils::log::RED, "ERR", $($t)*) };
    }

    pub use __error as error;
    pub use __info as info;
    pub use __warn as warn;
}

pub struct ProcessBuilder {
    cmd: Command,
    stream: bool,
}

pub struct Process {
    pub stdout: String,
    pub stderr: String,
    pub exit_code: Option<i32>,
}

pub fn process<I, S>(file_name: &str, args: I) -> ProcessBuilder
where
    I: IntoIterator<Item = S>,
    S: AsRef<OsStr>,
{
    let mut cmd = Command::new(file_name);
    cmd.args(args);
    cmd.current_dir(crate::path::git_root());
    ProcessBuilder { cmd, stream: false }
}

pub fn dedent(raw: &str) -> String {
    let lines: Vec<&str> = raw.lines().collect();

    let start = lines
        .iter()
        .position(|l| !l.trim().is_empty())
        .unwrap_or(lines.len());
    let end = lines
        .iter()
        .rposition(|l| !l.trim().is_empty())
        .map_or(start, |i| i + 1);
    let kept = &lines[start..end];
    if kept.is_empty() {
        return String::new();
    }

    let indent = kept
        .iter()
        .filter(|l| !l.trim().is_empty())
        .map(|l| l.len() - l.trim_start().len())
        .min()
        .unwrap_or(0);

    let mut out = String::new();
    for line in kept {
        if line.trim().is_empty() {
            out.push('\n');
        } else {
            out.push_str(&line[indent..]);
            out.push('\n');
        }
    }
    out
}

fn tee<R: Read, W: Write>(mut src: R, mut sink: W, echo: bool) -> Vec<u8> {
    let mut all = Vec::new();
    let mut buf = [0u8; 4096];
    loop {
        match src.read(&mut buf) {
            Ok(0) | Err(_) => break,
            Ok(n) => {
                all.extend_from_slice(&buf[..n]);
                if echo {
                    let _ = sink.write_all(&buf[..n]);
                    let _ = sink.flush();
                }
            }
        }
    }
    all
}

impl ProcessBuilder {
    pub fn cwd(mut self, dir: impl AsRef<std::path::Path>) -> Self {
        self.cmd.current_dir(dir);
        self
    }

    pub fn env(mut self, k: impl AsRef<OsStr>, v: impl AsRef<OsStr>) -> Self {
        self.cmd.env(k, v);
        self
    }

    pub fn stream(mut self) -> Self {
        self.stream = true;
        self
    }

    pub fn exec(mut self) -> io::Result<Process> {
        let stream = self.stream;
        let mut child = self
            .cmd
            .stdout(Stdio::piped())
            .stderr(Stdio::piped())
            .spawn()?;

        let out = child.stdout.take().expect("stdout was piped");
        let err = child.stderr.take().expect("stderr was piped");

        let out_t = thread::spawn(move || tee(out, io::stdout(), stream));
        let err_t = thread::spawn(move || tee(err, io::stderr(), stream));

        let status = child.wait()?;
        let stdout = out_t.join().expect("stdout thread panicked");
        let stderr = err_t.join().expect("stderr thread panicked");

        Ok(Process {
            stdout: String::from_utf8_lossy(&stdout).into_owned(),
            stderr: String::from_utf8_lossy(&stderr).into_owned(),
            exit_code: status.code(),
        })
    }

    pub fn exec_checked(self, what: &str) -> Process {
        let proc = self.stream().exec().unwrap_or_else(|e| {
            log::error!("failed to start {what}: {e}");
            std::process::exit(1);
        });
        if !proc.success() {
            log::error!("{what} failed ({:?})", proc.exit_code());
            std::process::exit(1);
        }
        proc
    }
}

impl Process {
    pub fn stdout(&self) -> &str {
        &self.stdout
    }
    pub fn stderr(&self) -> &str {
        &self.stderr
    }
    pub fn exit_code(&self) -> Option<i32> {
        self.exit_code
    }
    pub fn success(&self) -> bool {
        self.exit_code == Some(0)
    }
}
