{
  dotnetCorePackages,
  buildDotnetModule,
  makeRustPlatform,
  rustPlatform,
  pkg-config,
  fontconfig,
  stdenv,
  clang,
  zlib,
  krb5,
  dbus,
  lib,
}:
{
  fenix,
  ...
}:
let
  dotnet-sdk = dotnetCorePackages.sdk_10_0;
  dotnet-runtime = dotnetCorePackages.runtime_10_0-bin;
  toolchain =
    with fenix.packages.${stdenv.system};
    combine [ minimal.toolchain ];
  src = ../.;
  fenixRustPlatform = makeRustPlatform {
    cargo = toolchain;
    rustc = toolchain;
  };

  xtask = fenixRustPlatform.buildRustPackage {
    pname = "xtask";
    version = "0.0.0";
    src = ../build;
    cargoHash = "sha256-7YV+oYJE266v0iyzCZmRCo0bkLKLBMLXBKTIG7r03E0=";
    doCheck = false;
  };
  version = "2.0.6-beta.8";
in
buildDotnetModule {
  inherit dotnet-runtime dotnet-sdk src version;
  projectFile = "Froststrap/Froststrap.csproj";
  nugetDeps = ./deps.json;
  name = "froststrap";
  cargoRoot = "backend";
  selfContainedBuild = true;

  cargoDeps = rustPlatform.fetchCargoVendor {
    src = "${src}/backend";
    hash = "sha256-KqFpWnP1SgVWs//PM0nqgzpmxDTGuHhy2ffHils4U2Q=";
  };

  nativeBuildInputs = [
    rustPlatform.cargoSetupHook
    toolchain
    pkg-config
    clang
    xtask
  ];
  buildInputs = [ dbus fontconfig zlib krb5 ];

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
    makeWrapper $out/lib/froststrap/Froststrap $out/bin/Froststrap \
      --prefix LD_LIBRARY_PATH : ${lib.makeLibraryPath [ dbus fontconfig ]}:$out/lib/froststrap/libs
    runHook postInstall
  '';

  meta.mainProgram = "Froststrap";
}
