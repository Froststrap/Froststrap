{
  mkShell,
  lib,
  xorg,
  stdenv,
  expat,
  fontconfig,
  freetype,
  libGL,
  vulkan-loader,
  wayland,
  libxkbcommon,
  libxkbcommon_8,
  pkg-config,
  dotnetCorePackages,
  just,
  glib
}:
mkShell rec {
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
      # FIXME: Need to use new Nixpkgs spec for these,
      # this is deprecated
      xorg.libX11
      xorg.libICE
      xorg.libSM
      xorg.libXi
      xorg.libXrandr
      xorg.libXcursor
      xorg.libxcb
      xorg.xcbutil
    ];

  buildInputs = [
    dotnetCorePackages.sdk_10_0-bin
    just
  ] ++ lib.optionals stdenv.isLinux [
    glib
  ];

  nativeBuildInputs = lib.optionals stdenv.isLinux [
      pkg-config
      # FIXME: Need to use new Nixpkgs spec for these,
      # this is deprecated
      xorg.libxcb
      xorg.xcbutil
      libxkbcommon
      libxkbcommon_8
    ];

  LD_LIBRARY_PATH = lib.makeLibraryPath runtimeLibs;
}
