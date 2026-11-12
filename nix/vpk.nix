# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  lib,
  stdenv,
  dotnetCorePackages,
  buildDotnetGlobalTool,
  symlinkJoin,
  makeWrapper,
}:
let
  dotnet-sdk = dotnetCorePackages.sdk_10_0;
  dotnet-runtime = dotnetCorePackages.runtime_10_0-bin;

  vpk-unwrapped = buildDotnetGlobalTool {
    pname = "vpk";
    version = "1.2.161";
    nugetSha256 =
      if stdenv.hostPlatform.isDarwin then
        "sha256-rJvnOEbZp6dkg/hoSr0VA81QcDuZJydCnc3ARVicXgg="
      else if stdenv.hostPlatform.isLinux then
        "sha256-rJvnOEbZp6dkg/hoSr0VA81QcDuZJydCnc3ARVicXgg="
      else
        lib.fakeSha256;
    nugetName = "vpk";
    inherit dotnet-sdk dotnet-runtime;
  };
in
symlinkJoin {
  name = "vpk-${vpk-unwrapped.version}";
  paths = [ vpk-unwrapped ];
  nativeBuildInputs = [ makeWrapper ];
  postBuild = ''
    wrapProgram $out/bin/vpk \
      --set DOTNET_ROOT "${dotnet-runtime}/share/dotnet" \
      --set DOTNET_MULTILEVEL_LOOKUP 0 \
      --set DOTNET_ROLL_FORWARD LatestMajor \
      --prefix PATH : "${lib.makeBinPath [ dotnet-sdk ]}"
  '';
  meta = vpk-unwrapped.meta or { };
}
