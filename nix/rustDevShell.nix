# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  inputs,
  stdenv,
  callPackage,
  cargo-bloat,
}:
let
  inherit (callPackage ./devshell-tools.nix { }) mkFragment;
  inherit (inputs) fenix;
  toolchain =
    with fenix.packages.${stdenv.system};
    combine [
      latest.toolchain
    ];
in
mkFragment {
  buildInputs = [
    toolchain
    cargo-bloat
  ];
}
