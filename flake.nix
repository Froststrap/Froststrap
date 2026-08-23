# SPDX-License-Identifier: Unlicense

{
  description = "Flake for Froststrap";

  inputs = {
    fenix = {
      url = "github:nix-community/fenix";
      inputs.nixpkgs.follows = "nixpkgs";
    };
    flake-utils.url = "github:numtide/flake-utils";
    nixpkgs.url = "github:nixos/nixpkgs/nixos-unstable";
    treefmt-nix.url = "github:numtide/treefmt-nix";
    self.submodules = true;
  };

  outputs =
    {
      flake-utils,
      nixpkgs,
      treefmt-nix,
      ...
    }@inputs: flake-utils.lib.eachDefaultSystem(system: let
      pkgs = import nixpkgs { inherit system; };
    in {
      devShells = let
        inherit (pkgs.callPackage ./nix/devshell-tools.nix {}) mkComposedShell;
        dotnetFrag = pkgs.callPackage ./nix/dotnetDevShell.nix { };
        extraFrag = pkgs.callPackage ./nix/extra.nix { };
        rustFrag = pkgs.callPackage ./nix/rustDevShell.nix { inherit inputs; };
      in {
        default = mkComposedShell [ dotnetFrag rustFrag extraFrag ];
        dotnet = mkComposedShell [ dotnetFrag ];
        rust = mkComposedShell [ rustFrag ];
      };
      packages = rec {
        debug = pkgs.callPackage ./nix/build.nix {};
        default = debug;
      };
      formatter = (treefmt-nix.lib.evalModule pkgs (_: {
        projectRootFile = "flake.nix";
        programs = {
          nixfmt.enable = true;
          nixf-diagnose.enable = true;
        };
        settings.formatter = {
          dotnet-format = {
            command = "${pkgs.dotnetCorePackages.sdk_10_0-bin}/bin/dotnet";
            options = [
              "format"
            ];
            includes = [ "*.csproj" ];
          };
        };
      })).config.build;
    }
  );
}
