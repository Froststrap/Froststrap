{
  rpm,
  lib,
  dpkg,
  typos,
  stdenv,
  nushell,
  callPackage
}:
let
  inherit (callPackage ./devshell-tools.nix {}) mkFragment;
in
mkFragment {
  buildInputs = [
    nushell
    typos
  ] ++ lib.optionals stdenv.hostPlatform.isLinux [
    rpm
    dpkg
  ];
}
