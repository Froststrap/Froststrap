# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  lib,
  stdenv,
  callPackage,
  pkg-config,
  dbus,
  libpulseaudio,
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
      latest.cargo
      latest.rustc
      latest.clippy
      latest.rustfmt
      latest.rust-src
      latest.rust-analyzer
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
  ]
  ++ lib.optionals stdenv.hostPlatform.isLinux [
    libpulseaudio
  ];
}
