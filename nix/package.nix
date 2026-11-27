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
  libpulseaudio,
  openssl,
  apple-sdk,
  darwin,
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
    libpulseaudio
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

  executables = if stdenv.hostPlatform.isDarwin then [ ] else null;

  makeWrapperArgs = [
    "--prefix"
    "LD_LIBRARY_PATH"
    ":"
    "${placeholder "out"}/lib/froststrap/libs"
  ];

  cargoDeps = rustPlatform.fetchCargoVendor {
    src = "${src}/backend";
    hash = "sha256-ofcuybSYLCp9WLqYLowJ4Di6aHGkU3Sv0NwjY1p4a2I=";
  };

  nativeBuildInputs = [
    rustPlatform.cargoSetupHook
    toolchain
    xtask
    clang
  ]
  ++ lib.optionals stdenv.hostPlatform.isLinux [
    pkg-config
  ];

  buildInputs =
    lib.optionals stdenv.hostPlatform.isLinux [
      dbus
      libpulseaudio
      fontconfig
      zlib
      krb5
    ]
    ++ lib.optionals stdenv.hostPlatform.isDarwin [
      apple-sdk
      darwin.ICU
    ];

  buildPhase = ''
    runHook preBuild
    export CARGO_NET_OFFLINE=true
    xtask publish --no-installers --output "$PWD/build"
    runHook postBuild
  '';

  installPhase =
    if stdenv.hostPlatform.isDarwin then
      ''
        runHook preInstall
        mkdir -p $out/Applications $out/bin
        cp -R build/dist/Froststrap.app $out/Applications/
        makeWrapper $out/Applications/Froststrap.app/Contents/MacOS/froststrap $out/bin/froststrap
        runHook postInstall
      ''
    else
      ''
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
      "aarch64-darwin"
    ];
  };
}
