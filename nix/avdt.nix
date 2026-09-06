{
  buildDotnetGlobalTool,
  writeShellScriptBin,
  dotnetCorePackages,
}:
let
  dotnet-sdk = dotnetCorePackages.sdk_8_0;
  avdt-unwrapped = buildDotnetGlobalTool {
    pname = "avdt";
    version = "2.2.3";
    nugetSha256 = "K0cjbRCBabODRwua7BsNEEO++4jPNIsW9+bwHDtsNz8=";
    nugetName = "avaloniaui.developertools.macos";
    dotnet-sdk = dotnet-sdk;
  };
in
writeShellScriptBin "avdt" ''
  DATA_DIR="''${XDG_DATA_HOME:-$HOME/.local/share}/avdt-2.2.3"
  if [ ! -d "$DATA_DIR" ]; then
    mkdir -p "$DATA_DIR"
    cp -r ${avdt-unwrapped}/lib/avdt/. "$DATA_DIR"
    chmod -R u+w "$DATA_DIR"
  fi
  export DOTNET_ROOT="${dotnet-sdk}/share/dotnet"
  exec "$DATA_DIR/avdt" "$@"
''
