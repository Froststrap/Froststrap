# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  lib,
  nfpm,
  zstd,
  stdenv,
  squashfsTools,
  callPackage,
}:
{
  ...
}@inputs:
let
  inherit (callPackage ./devshell-tools.nix { }) mkFragment;
  avdt = (callPackage ./avdt.nix { }) inputs;
  vpk = (callPackage ./vpk.nix { }) inputs;
in
mkFragment {
  name = "extra";
  buildInputs = [
    vpk
    avdt
    zstd
  ]
  ++ lib.optionals stdenv.hostPlatform.isLinux [
    nfpm
    squashfsTools
  ];
}
