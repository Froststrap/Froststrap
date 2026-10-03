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
in
mkFragment {
  name = "extra";
  buildInputs = [
    reuse
    typos
  ]
  ++ lib.optionals stdenv.hostPlatform.isLinux [
    nfpm
  ];
}
