# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  callPackage,
  omnisharp-roslyn,
}:
let
  inherit (callPackage ./devshell-tools.nix { }) mkFragment;
in
mkFragment {
  name = "lsp";
  buildInputs = [
    omnisharp-roslyn
  ];
}
