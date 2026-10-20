//! Arg handling

use std::{
    fmt::{Display, Formatter},
    sync::OnceLock,
};

use std::str::FromStr;
use crate::{action::Action, utils::log};

const VERSION: &str = env!("CARGO_PKG_VERSION");

const HELP: &str = "\
Build system for Froststrap

Usage: xtask <ACTION> [OPTIONS]

Action:
  clean      Delete the output directory
  restore    Run `dotnet restore`
  build      Run `dotnet build`
  publish    Build a shippable executable

Options:
  --configuration <debug|release>  Base configuration (default: release)
  --override-arch <ARCH>           Cross-compile for another arch
  --no-installers                  Skip installers on publish
  -h, --help                       Print this message
  -v, --version                    Print version
";

#[derive(Debug)]
pub struct AppArgs {
    pub action: crate::action::Action,
    pub no_installers: bool,
    pub configuration: Configuration,
    pub override_arch: Option<String>,
}

impl AppArgs {
    pub fn parse() -> Self {
        fn die(msg: impl Display) -> ! {
            log::error!("{msg}\n{HELP}");
            std::process::exit(1);
        }

        let mut pargs = pico_args::Arguments::from_env();

        if pargs.contains(["-h", "--help"]) {
            print!("{HELP}");
            std::process::exit(0);
        }

        if pargs.contains(["-v", "--version"]) {
            print!("{VERSION}");
            std::process::exit(0);
        }

        let no_installers = pargs.contains("--no-installers");

        let configuration = pargs
            .opt_value_from_str("--configuration")
            .unwrap_or_else(|e| die(format!("--configuration: {e}")))
            .unwrap_or_default();

        let override_arch = pargs
            .opt_value_from_str("--override-arch")
            .unwrap_or_else(|e| die(format!("--override-arch: {e}")));

        let action = pargs
            .free_from_str::<Action>()
            .unwrap_or_else(|e| die(format!("<ACTION>: {e}")));

        if let Some(extra) = pargs.finish().first() {
            die(format!("unknown argument `{}`", extra.to_string_lossy()));
        }

        Self { action, no_installers, configuration, override_arch }
    }
}

#[derive(Clone, Debug, Copy, PartialEq, Eq, Default)]
pub enum Configuration {
    Debug,
    #[default]
    Release,
}

impl FromStr for Configuration {
    type Err = String;

    fn from_str(v: &str) -> Result<Self, Self::Err> {
        match v.to_ascii_lowercase().as_str() {
            "debug" => Ok(Self::Debug),
            "release" => Ok(Self::Release),
            _ => Err(format!("unknown configuration `{v}` (expected debug or release)")),
        }
    }
}

impl Display for Configuration {
    fn fmt(&self, f: &mut Formatter<'_>) -> std::fmt::Result {
        use Configuration::*;
        f.write_str(match self {
            Debug => "debug",
            Release => "release",
        })
    }
}

static ARGS: OnceLock<AppArgs> = OnceLock::new();

pub fn init() {
    let args = AppArgs::parse();
    ARGS.set(args).expect("args already initialized");
}

pub fn get() -> &'static AppArgs {
    ARGS.get().expect("args not initialized, call init() first")
}
