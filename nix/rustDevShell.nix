# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  stdenv,
  callPackage,
  cargo-bloat,
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
      latest.toolchain
    ];
in
mkFragment {
  name = "rust";
  buildInputs = [
    toolchain
    cargo-bloat
  ];
  nativeBuildInputs = [
    pkg-config
    dbus
  ];
}
