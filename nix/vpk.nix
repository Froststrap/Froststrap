# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  lib,
  stdenv,
  dotnetCorePackages,
  buildDotnetGlobalTool,
}:
let
  dotnet-sdk = dotnetCorePackages.sdk_10_0;
  vpk-unwrapped = buildDotnetGlobalTool {
    pname = "vpk";
    version = "1.2.161";
    nugetSha256 =
      if stdenv.hostPlatform.isDarwin then
        "sha256-rJvnOEbZp6dkg/hoSr0VA81QcDuZJydCnc3ARVicXgg="
      else if stdenv.hostPlatform.isLinux then
        "sha256-rJvnOEbZp6dkg/hoSr0VA81QcDuZJydCnc3ARVicXgg="
      else
        "";
    nugetName =
      "vpk";
    dotnet-sdk = dotnet-sdk;
  };
in
vpk-unwrapped
