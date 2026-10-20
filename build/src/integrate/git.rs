use std::sync::OnceLock;

use crate::utils::process;

pub struct GitInfo {
    pub commit: String,
    pub branch: String,
    pub dir: String,
    pub tag: Option<String>,
}

pub fn git(args: &[&str]) -> Option<String> {
    let p = process("git", args.iter().copied()).exec().ok()?;
    if !p.success() {
        return None;
    }
    let out = p.stdout().trim();
    (!out.is_empty()).then(|| out.to_string())
}

pub fn info() -> &'static GitInfo {
    static INFO: OnceLock<GitInfo> = OnceLock::new();
    INFO.get_or_init(|| {
        let branch = git(&["rev-parse", "--abbrev-ref", "HEAD"])
            .filter(|b| b != "HEAD")
            .or_else(|| {
                std::env::var("GITHUB_HEAD_REF")
                    .ok()
                    .filter(|s| !s.is_empty())
            })
            .or_else(|| {
                (std::env::var("GITHUB_REF_TYPE").as_deref() == Ok("branch"))
                    .then(|| std::env::var("GITHUB_REF_NAME").ok())
                    .flatten()
            })
            .unwrap_or_else(|| "detached".into());

        GitInfo {
            commit: git(&["rev-parse", "HEAD"]).unwrap_or_else(|| "unknown".into()),
            branch,
            dir: crate::path::git_root().to_string_lossy().into_owned(),
            tag: git(&["describe", "--tags", "--exact-match"])
                .or_else(|| git(&["describe", "--tags", "--abbrev=0"])),
        }
    })
}
