{
  callPackage
}:
let
  inherit (callPackage ./devshell-tools.nix {}) mkFragment;
in
mkFragment {
  shellHook = ''
    if [ -d "/Applications/Xcode-beta.app" ]; then
      export DEVELOPER_DIR="/Applications/Xcode-beta.app/Contents/Developer"
    else
      export DEVELOPER_DIR="/Applications/Xcode.app/Contents/Developer"
    fi
    export SDKROOT="$(/usr/bin/xcrun --sdk macosx --show-sdk-path)"
  '';
}
