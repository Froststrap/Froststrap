{
  lib,
  stdenv,
  dotnetCorePackages,
  writeShellScriptBin,
  buildDotnetGlobalTool
}:
let
  dotnet-sdk = dotnetCorePackages.sdk_10_0;
  avdt-unwrapped = buildDotnetGlobalTool {
    pname = "avdt";
    version = "2.2.3";
    nugetSha256 = if stdenv.hostPlatform.isDarwin then
      "K0cjbRCBabODRwua7BsNEEO++4jPNIsW9+bwHDtsNz8="
    else if stdenv.hostPlatform.isLinux then
      "e46swp/RGRZXmdremASzqQ6+Qrs90gabrY5lWP7y/p8="
    else "";
    nugetName =
      "avaloniaui.developertools."
      + lib.optionalString stdenv.hostPlatform.isLinux "linux"
      + lib.optionalString stdenv.hostPlatform.isDarwin "macos";
    dotnet-sdk = dotnet-sdk;
  };
in
writeShellScriptBin "avdt" (
  if stdenv.hostPlatform.isDarwin then ''
    DATA_DIR="''${XDG_DATA_HOME:-$HOME/.local/share}/avdt-2.2.3"
    if [ ! -d "$DATA_DIR" ]; then
      mkdir -p "$DATA_DIR"
      cp -r ${avdt-unwrapped}/lib/avdt/. "$DATA_DIR"
      chmod -R u+w "$DATA_DIR"
    fi
    export DOTNET_ROOT="${dotnet-sdk}/share/dotnet"
    exec "$DATA_DIR/avdt" "$@"
  '' else ''
    exec ${avdt-unwrapped}/bin/avdt "$@"
  ''
)
