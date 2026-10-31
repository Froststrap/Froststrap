# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0

{
  description = "Flake for Froststrap";

  nixConfig = {
    extra-substituters = [ "https://invra.cachix.org" ];
    extra-trusted-public-keys = [
      "invra.cachix.org-1:5lB/b5n5iQVLCbAYfnH5f5ASzqlVB6uKfINHge7lVvk="
    ];
  };

  inputs = {
    fenix = {
      url = "github:nix-community/fenix";
      inputs.nixpkgs.follows = "nixpkgs";
    };
    flake-utils.url = "github:numtide/flake-utils";
    nixpkgs.url = "github:nixos/nixpkgs/26.05";
    nixpkgs-unstable.url = "github:nixos/nixpkgs/nixpkgs-unstable";
    treefmt-nix.url = "github:numtide/treefmt-nix";
    self.submodules = true;
  };

  outputs =
    {
      flake-utils,
      nixpkgs,
      nixpkgs-unstable,
      ...
    }@inputs:
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = import nixpkgs {
          inherit system;
        };

        pkgs-unstable = import nixpkgs-unstable {
          inherit system;
        };
      in
      {
        devShells =
          let
            inherit (pkgs.callPackage ./nix/devshell-tools.nix { })
              mkComposedShell
              ;

            dotnetFrag = pkgs.callPackage ./nix/dotnetDevShell.nix {
              fontconfig = pkgs-unstable.fontconfig;
            };
            extraFrag = pkgs.callPackage ./nix/extra.nix { };
            lintFrag = pkgs.callPackage ./nix/lint.nix { };
            lspFrag = pkgs.callPackage ./nix/lsp.nix { };
            rustFrag = (pkgs.callPackage ./nix/rustDevShell.nix { }) inputs;
          in
          mkComposedShell [ rustFrag ] { }
          // mkComposedShell [ dotnetFrag ] { }
          // mkComposedShell [ extraFrag ] { }
          // mkComposedShell [ lintFrag ] { }
          // mkComposedShell [ dotnetFrag lspFrag ] { nameOverride = "lsp"; }
          // mkComposedShell [ dotnetFrag rustFrag ] { nameOverride = "minimal"; }
          // mkComposedShell [
            dotnetFrag
            rustFrag
            extraFrag
            lintFrag
          ] { nameOverride = "default"; };

        packages = let
          fontconfig = pkgs-unstable.fontconfig;
        in rec {
          froststrap = (pkgs.callPackage ./nix/package.nix {
            inherit fontconfig;
          }) inputs;
          froststrap-bin = pkgs.callPackage ./nix/binary.nix {
            inherit fontconfig;
          };
          default = froststrap;
        };

        formatter = (pkgs.callPackage ./nix/formatter.nix { }) inputs;
      }
    )
    // {
      overlays.default = final: prev: {
        froststrap = final.callPackage ./nix/package.nix { };
        mkFroststrap = args: final.callPackage ./nix/binary.nix args;
      };
    };
}

