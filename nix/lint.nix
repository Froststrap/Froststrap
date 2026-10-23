# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  callPackage,
  reuse,
  typos,
}:
let
  inherit (callPackage ./devshell-tools.nix { }) mkFragment;
in
mkFragment {
  name = "lint";
  buildInputs = [
    reuse
    typos
  ];
}
