#   SPDX-License-Identifier: Unlicense

{
  description = "Flake for Froststrap";

  inputs = {
    self.submodules = true;
    nixpkgs.url = "github:nixos/nixpkgs/nixos-unstable";
    treefmt-nix.url = "github:numtide/treefmt-nix";
  };

  outputs =
    {
      nixpkgs,
      treefmt-nix,
      ...
    }:
    let
      forAllSystems = nixpkgs.lib.genAttrs nixpkgs.lib.systems.flakeExposed;
    in
      {
        devShells = forAllSystems (system: let
          pkgs = import nixpkgs { inherit system; };
        in rec {
          froststrap = pkgs.callPackage ./nix/devShell.nix {};
          default = froststrap;
        });
        packages = forAllSystems (system: let
          pkgs = import nixpkgs { inherit system; };
        in rec {
          debug = pkgs.callPackage ./nix/build.nix {};
          default = debug;
        });
        formatter = forAllSystems (system:
          let
            pkgs = import nixpkgs { inherit system; };
          in
          (treefmt-nix.lib.evalModule pkgs (_: {
            projectRootFile = "flake.nix";
            programs = {
              nixfmt.enable = true;
              nixf-diagnose.enable = true;
              toml-sort.enable = true;
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
          })).config.build
        );
      };
}
