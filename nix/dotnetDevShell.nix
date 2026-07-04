{
  mkShell,
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
  just,
  glib,
  omnisharp-roslyn,
  create-dmg
}:
mkShell (finalAttrs: {
  meta.license = lib.licenses.unlicense;
  runtimeLibs = lib.optionals stdenv.isLinux [
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
    just
  ] ++ lib.optionals stdenv.isLinux [
    glib
  ] ++ lib.optionals stdenv.isDarwin [
    create-dmg
  ];

  nativeBuildInputs = lib.optionals stdenv.isLinux [
    pkg-config
    libxcb
    xcbutil
    libxkbcommon
  ];

  LD_LIBRARY_PATH = lib.makeLibraryPath finalAttrs.runtimeLibs;
})
