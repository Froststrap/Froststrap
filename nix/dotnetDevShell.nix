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
  dotnetCorePackages,
  glib,
  omnisharp-roslyn,
  callPackage,
}:
let
  inherit (callPackage ./devshell-tools.nix {}) mkFragment;
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
    dotnetCorePackages.sdk_10_0-bin
    omnisharp-roslyn # lsp
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
  '';
})
