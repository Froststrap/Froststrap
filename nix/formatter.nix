# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0

{
  pkgs,
  ...
}:
{
  treefmt-nix,
  ...
}:
(treefmt-nix.lib.evalModule pkgs (_: {
  projectRootFile = "flake.nix";

  programs = {
    nixfmt.enable = true;
    nixf-diagnose.enable = true;
  };
})).config.build.wrapper
