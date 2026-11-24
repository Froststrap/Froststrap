# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0

{
  buildDotnetModule,
  makeRustPlatform,
  rustPlatform,
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
  version = lib.pipe (builtins.readFile ../Froststrap/Froststrap.csproj) [
    (lib.splitString "\n")
    (map (builtins.match "[[:space:]]*<AppVersion[^>]*>([^<]+)</AppVersion>.*"))
    (lib.findFirst (m: m != null) (throw "Froststrap.csproj has no <AppVersion>"))
    builtins.head
  ];

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
    export CARGO_NET_OFFLINE=true
    xtask publish --no-installers --output "$PWD/build"
    runHook postBuild
  '';

  installPhase = ''
    runHook preInstall
    mkdir -p $out/lib/froststrap $out/bin
    cp -r build/publish/. $out/lib/froststrap/
    install -Dm644 build/dist/froststrap.desktop $out/share/applications/froststrap.desktop
    install -Dm644 ${src}/build/icon512.png $out/share/icons/hicolor/512x512/apps/froststrap.png
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
