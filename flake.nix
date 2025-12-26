#   SPDX-License-Identifier: Unlicense

{
  description = "Flake for Froststrap";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
    treefmt-nix.url = "github:numtide/treefmt-nix";
    csharp-ls.url = "github:invra/csharp-language-server";
  };

  outputs =
    {
      nixpkgs,
      flake-utils,
      treefmt-nix,
      csharp-ls,
      ...
    }:
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = import nixpkgs {
          inherit system;
          overlays = [ csharp-ls.overlays.default ];
        };

        formatters =
          (treefmt-nix.lib.evalModule pkgs (_: {
            projectRootFile = ".git/config";
            programs = {
              nixfmt.enable = true;
              nixf-diagnose.enable = true;
              toml-sort.enable = true;
              rustfmt.enable = true;
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
      in
      {
        devShells.default = pkgs.mkShell {
          meta.license = pkgs.lib.licenses.unlicense;
          buildInputs = with pkgs; [
            dotnetCorePackages.sdk_10_0-bin
            csharp-language-server
            just
          ];
        };
      }
    );
}

