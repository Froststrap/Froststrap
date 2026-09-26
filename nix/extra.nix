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
  linuxdeploy,
}:
let
  inherit (callPackage ./devshell-tools.nix {}) mkFragment;
in
mkFragment {
  buildInputs = [
    reuse
    typos
  ] ++ lib.optionals stdenv.hostPlatform.isLinux [
    linuxdeploy
    nfpm
  ];
}
