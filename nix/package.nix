# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0

{
  buildDotnetModule,
  makeRustPlatform,
  rustPlatform,
  makeDesktopItem,
  pkg-config,
  fontconfig,
  freetype,
  libGL,
  nspr,
  vulkan-loader,
  wayland,
  libxkbcommon,
  libsecret,
  skia,
  libX11,
  libICE,
  libSM,
  libXi,
  libXrandr,
  libxcursor,
  libxcb,
  xcbutil,
  glib,
  stdenv,
  expat,
  clang,
  zlib,
  krb5,
  dbus,
  openssl,
  lib,
}:
{
  dotnix,
  fenix,
  ...
}:
let
  dotnet = dotnix.packages.${stdenv.hostPlatform.system};
  dotnet-sdk = dotnet.combine [ dotnet."10-0".stable.sdk ];
  dotnet-runtime = dotnet.combine [ dotnet."10-0".stable.runtime ];
  toolchain = with fenix.packages.${stdenv.system}; combine [ minimal.toolchain ];
  src = ../.;
  fenixRustPlatform = makeRustPlatform {
    cargo = toolchain;
    rustc = toolchain;
  };

  xtask = fenixRustPlatform.buildRustPackage {
    pname = "xtask";
    version = "0.0.0";
    src = ../build;
    cargoHash = "sha256-WTz+k1t9M/sVZrPFResdpWo9DeheRFZ5u9xSNuJjWrY=";
    doCheck = false;
  };
  version = "2.0.9-beta.1";

  runtimeDeps = lib.optionals stdenv.hostPlatform.isLinux [
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
    openssl

    # X11 libs
    libX11
    libICE
    libSM
    libXi
    libXrandr
    libxcursor
    libxcb
    xcbutil
    glib
  ];

  desktopItem = makeDesktopItem {
    name = "froststrap-launcher"; # needed because for some reason on startup froststrap sets NoDisplay=true into ~/.local/share/applications/froststrap.desktop which takes priority
    desktopName = "Froststrap";
    comment = "A cross-platform Roblox bootstrapper, focused on performance and customization.";
    exec = "froststrap %u";
    icon = "froststrap";
    terminal = false;
    startupNotify = true;
    categories = [ "Game" ];
    mimeTypes = [
      "x-scheme-handler/roblox"
      "x-scheme-handler/roblox-player"
      "x-scheme-handler/roblox-studio"
      "x-scheme-handler/roblox-studio-auth"
    ];
  };
in
buildDotnetModule {
  inherit
    dotnet-runtime
    dotnet-sdk
    src
    version
    runtimeDeps
    ;
  projectFile = "Froststrap/Froststrap.csproj";
  nugetDeps = ./deps.json;
  name = "froststrap";
  pname = "froststrap";
  dotnetInstallPath = "$out/lib/froststrap";
  cargoRoot = "backend";
  selfContainedBuild = true;

  makeWrapperArgs = [
    "--prefix"
    "LD_LIBRARY_PATH"
    ":"
    "${placeholder "out"}/lib/froststrap/libs"
  ];

  cargoDeps = rustPlatform.fetchCargoVendor {
    src = "${src}/backend";
    hash = "sha256-/pyk7zjH7vZ+M3Zh/iaUW2jaewZp0yURE6ePsoK/fY8=";
  };

  nativeBuildInputs = [
    rustPlatform.cargoSetupHook
    toolchain
    xtask
  ]
  ++ lib.optionals stdenv.hostPlatform.isLinux [
    pkg-config
    clang
  ];

  buildInputs = lib.optionals stdenv.hostPlatform.isLinux [
    dbus
    fontconfig
    zlib
    krb5
  ];

  buildPhase = ''
    runHook preBuild
    export FROSTSTRAP_VERSION=${version}
    export CARGO_NET_OFFLINE=true
    xtask publish --no-installers --output "$PWD/build/publish"
    runHook postBuild
  '';

  installPhase = ''
    runHook preInstall
    mkdir -p $out/lib/froststrap $out/bin
    cp -r build/publish/publish/. $out/lib/froststrap/
    install -Dm644 ${desktopItem}/share/applications/froststrap-launcher.desktop $out/share/applications/froststrap-launcher.desktop
    install -Dm644 ${src}/build/icon512.png $out/share/icons/hicolor/512x512/apps/froststrap.png
    sed -i -E '/^Categories=/ s/([^;])$/\1;/' $out/share/applications/froststrap-launcher.desktop
    runHook postInstall
  '';

  meta = {
    description = "A cross-platform Roblox bootstrapper, focused on performance and customization.";
    homepage = "https://froststrap.xyz/";
    license = lib.licenses.mpl20;
    mainProgram = "froststrap";
    platforms = [
      "x86_64-linux"
      "x86_64-darwin"
      "aarch64-darwin"
    ];
  };
}
