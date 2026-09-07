# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  rpm,
  lib,
  dpkg,
  typos,
  stdenv,
  nushell,
  callPackage
}:
let
  inherit (callPackage ./devshell-tools.nix {}) mkFragment;
in
mkFragment {
  buildInputs = [
    nushell
    typos
  ] ++ lib.optionals stdenv.hostPlatform.isLinux [
    rpm
    dpkg
  ];
}
