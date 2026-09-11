# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  lib,
  stdenv,
  expat,
  fontconfig,
  freetype,
  libGL,
  vulkan-loader,
  wayland,
  libxkbcommon,
  pkg-config,
  libX11,
  libICE,
  libXi,
  libXrandr,
  libSM,
  libxcb,
  xcbutil,
  libxcursor,
  libsecret,
  dotnetCorePackages,
  glib,
  omnisharp-roslyn,
  callPackage,
}:
let
  inherit (callPackage ./devshell-tools.nix {}) mkFragment;
  avdt = callPackage ./avdt.nix {};
  dotnet-tc = dotnetCorePackages.sdk_10_0-bin;
in
mkFragment (finalAttrs: {
  runtimeLibs = lib.optionals stdenv.hostPlatform.isLinux [
    expat
    fontconfig
    freetype
    libGL
    vulkan-loader
    wayland
    libxkbcommon
    libsecret

    # X11 libs
    libX11
    libICE
    libSM
    libXi
    libXrandr
    libxcursor
    libxcb
    xcbutil
  ];

  buildInputs = [
    omnisharp-roslyn # lsp
    dotnet-tc
    avdt # devtools for avalonia
  ] ++ lib.optionals stdenv.hostPlatform.isLinux [
    glib
  ];

  nativeBuildInputs = lib.optionals stdenv.hostPlatform.isLinux [
    pkg-config
    libxcb
    xcbutil
    libxkbcommon
  ];

  shellHook = ''
    export LD_LIBRARY_PATH=${lib.makeLibraryPath finalAttrs.runtimeLibs}
    export DOTNET_ROOT=${dotnet-tc}/share/dotnet
  '';
})
