# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  stdenv,
  callPackage,
  cargo-bloat,
}:
{
  fenix,
  ...
}:
let
  inherit (callPackage ./devshell-tools.nix { }) mkFragment;
  toolchain =
    with fenix.packages.${stdenv.system};
    combine [
      minimal.toolchain
      latest.clippy
      latest.rust-analyzer
    ];
in
mkFragment {
  name = "rust";
  buildInputs = [
    toolchain
    cargo-bloat
  ];
}
