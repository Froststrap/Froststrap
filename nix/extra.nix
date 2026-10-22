# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  lib,
  nfpm,
  stdenv,
  cargo-bloat,
  callPackage,
}:
let
  inherit (callPackage ./devshell-tools.nix { }) mkFragment;
  avdt = callPackage ./avdt.nix { };
  vpk = callPackage ./vpk.nix { };
in
mkFragment {
  name = "extra";
  buildInputs = [
    vpk # velopack tooling
    avdt # avalonia devtools
    cargo-bloat
  ]
  ++ lib.optionals stdenv.hostPlatform.isLinux [
    nfpm
  ];
}