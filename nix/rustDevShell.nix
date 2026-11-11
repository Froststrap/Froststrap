# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  stdenv,
  callPackage,
  pkg-config,
  dbus,
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
      nightly.cargo
      nightly.rustc
      nightly.clippy
      nightly.rustfmt
      nightly.rust-src
      nightly.rust-analyzer
  ];
in
mkFragment {
  name = "rust";
  buildInputs = [
    toolchain
  ];
  nativeBuildInputs = [
    pkg-config
    dbus
  ];
}
