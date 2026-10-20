//! Module holding all "actions" avalailable
//!
//! Export the module with a main function.

pub mod clean;
pub mod build;
pub mod restore;
pub mod publish;

use std::str::FromStr;

#[derive(Debug, Copy, Clone)]
pub enum Action {
    Clean,
    Restore,
    Build,
    Publish,
}

impl FromStr for Action {
    type Err = String;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "clean" => Ok(Self::Clean),
            "restore" => Ok(Self::Restore),
            "build" => Ok(Self::Build),
            "publish" => Ok(Self::Publish),
            _ => Err(format!("unknown command `{s}`")),
        }
    }
}

impl Action {
    pub fn exec(self) {
        match self {
            Action::Build => build::main(),
            Action::Restore => restore::main(),
            Action::Clean => clean::main(),
            Action::Publish => publish::main(),
        }
    }
}
