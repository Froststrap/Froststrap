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
      self,
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

        buildInputs = with pkgs; [
          dotnetCorePackages.sdk_10_0-bin
          csharp-language-server
          just
        ] ++ lib.optionals pkgs.stdenv.isLinux [
          glib
        ];

        nativeBuildInputs =
          with pkgs;
          lib.optionals pkgs.stdenv.isLinux [
            pkg-config
            xorg.libxcb
            xorg.xcbutil
            libxkbcommon
            libxkbcommon_8
          ];

        runtimeLibs =
          with pkgs;
          lib.optionals pkgs.stdenv.isLinux [
            expat
            fontconfig
            freetype
            libGL
            vulkan-loader
            wayland
            libxkbcommon

            # X11 libs
            xorg.libX11
            xorg.libICE
            xorg.libSM
            xorg.libXi
            xorg.libXrandr
            xorg.libXcursor
            xorg.libxcb
            xorg.xcbutil
          ];
        LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath runtimeLibs;
        DYLD_LIBRARY_PATH = pkgs.lib.makeLibraryPath runtimeLibs;
      in
      {
        devShells.default = pkgs.mkShell {
          meta.license = pkgs.lib.licenses.unlicense;
          inherit nativeBuildInputs buildInputs LD_LIBRARY_PATH DYLD_LIBRARY_PATH;
        };

        formatter = formatters.wrapper;
        checks.formatting = formatters.check self;
      }
    );
}

