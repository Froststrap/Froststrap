{
  mkShell,
  inputs,
  stdenv
}:
let
  inherit (inputs)fenix;
  toolchain = with fenix.packages.${stdenv.system}; combine [
    latest.toolchain
  ];
in
mkShell
{
  buildInputs = [
    toolchain
  ];
}
