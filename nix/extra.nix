# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  lib,
  nfpm,
  typos,
  reuse,
  stdenv,
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
    reuse
    typos
    avdt # avalonia devtools
    vpk # velopack tooling
  ]
  ++ lib.optionals stdenv.hostPlatform.isLinux [
    nfpm
  ];
}
