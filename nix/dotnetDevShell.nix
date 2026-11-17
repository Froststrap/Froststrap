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
  icu,
  libXi,
  libXrandr,
  libSM,
  libxcb,
  xcbutil,
  libxcursor,
  libsecret,
  glib,
  callPackage,
  nspr,
  dbus,
  openssl,
  squashfsTools,
  skia,
}:
{
  dotnix,
  ...
}:
let
  inherit (callPackage ./devshell-tools.nix { }) mkFragment;
  dotnet = dotnix.packages.${stdenv.hostPlatform.system};
  dotnet-tc = dotnet.combine [
    dotnet."10-0".stable.sdk
  ];
in
mkFragment (finalAttrs: {
  name = "dotnet";
  runtimeLibs = lib.optionals stdenv.hostPlatform.isLinux [
    expat
    fontconfig
    freetype
    libGL
    nspr
    vulkan-loader
    wayland
    libxkbcommon
    libsecret
    skia
    dbus

    # X11 libs
    libX11
    libICE
    libSM
    libXi
    libXrandr
    libxcursor
    libxcb
    xcbutil
    icu
    glib
    openssl
  ];

  buildInputs = [
    dotnet-tc
  ]
  ++ lib.optionals stdenv.hostPlatform.isLinux [
    glib
  ];

  nativeBuildInputs = lib.optionals stdenv.hostPlatform.isLinux [
    pkg-config
    libxcb
    xcbutil
    libxkbcommon

    squashfsTools
  ];

  shellHook = ''
    export LD_LIBRARY_PATH=${lib.makeLibraryPath finalAttrs.runtimeLibs}
    export DOTNET_ROOT=${dotnet-tc}/share/dotnet
  '';
})
