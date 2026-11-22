# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  callPackage,
  stdenv,
}:
{
  dotnix,
  fenix,
  ...
}:
let
  inherit (callPackage ./devshell-tools.nix { }) mkFragment;
  dotnet = dotnix.packages.${stdenv.system};
  rust-analyzer =
    with fenix.packages.${stdenv.system};
    combine [
      latest.rust-analyzer
      latest.rust-src
    ];
in
mkFragment {
  name = "lsp";
  buildInputs = [
    dotnet.roslyn-language-server
    rust-analyzer
  ];
}
