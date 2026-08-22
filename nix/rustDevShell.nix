{
  inputs,
  stdenv,
  callPackage,
}:
let
  inherit (callPackage ./devshell-tools.nix {}) mkFragment;
  inherit (inputs)fenix;
  toolchain = with fenix.packages.${stdenv.system}; combine [
    latest.toolchain
  ];
in
mkFragment
{
  buildInputs = [
    toolchain
  ];
}
