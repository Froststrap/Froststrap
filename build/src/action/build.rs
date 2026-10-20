//! build action

use crate::{
    arch, path, args,
    utils::{log, process},
};

pub fn main() {
    super::restore::main();
    let args = args::get();

    log::info!("Executing Build");
    let path: String = path::git_root()
        .join("Froststrap/Froststrap.csproj")
        .into_string()
        .unwrap();
    log::info!("Froststrap path: {}", path);
    let config = args.configuration;
    let output_path = path::dotnet_build_artifacts().into_string().unwrap();
    let rust_target_triple = arch::rust_triple();

    let args = [
        "msbuild".to_string(),
        path,
        format!("-p:Configuration={config}"),
        format!("-p:OutputPath={output_path}"),
        format!("-p:RustTargetTriple={rust_target_triple}"),
        "-nologo".to_string(),
    ];

    std::fs::create_dir_all(path::dotnet_build_artifacts()).unwrap();
    path::write_output_gitignore();
    process("dotnet", args).exec_checked("dotnet msbuild");
}
