# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0

{
  fontconfig,
  callPackage,
  version ? "2.0.8",
  sha256 ? "bVuA5O2l5OecjXS+Uo48a3wL3pU/yH09lZN5DtfUNRU=",
}:
callPackage ./appimage-bin-builder.nix {
  inherit version sha256 fontconfig;
}
