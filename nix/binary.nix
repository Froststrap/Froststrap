{
  fontconfig,
  callPackage,
  version ? "2.0.5",
  sha256 ? "1Dmx95czka04T3FtVpP2iMVQm2dXuQIhDx0flKOKyeg=",
}:
callPackage ./appimage-bin-builder.nix {
  inherit version sha256 fontconfig;
}
