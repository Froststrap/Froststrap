{
  lib,
  appimageTools,
  fetchurl,
  makeDesktopItem,
}:

let
  version = "2.0.2";

  src = fetchurl {
    url = "https://github.com/Froststrap/Froststrap/releases/download/v${version}/Froststrap-linux-x64.AppImage";
    hash = "sha256-KajUNB3vgzslBOTvL/GWTwRLZmbc+XKvtkyRQAz0ktE=";
  };

  appimageContents = appimageTools.extractType2 {
    inherit version src;
    pname = "froststrap";
  };

  desktopItem = makeDesktopItem {
    name = "froststrap";
    desktopName = "Froststrap";
    comment = "A cross-platform Roblox bootstrapper, focused on performance and customization.";
    exec = "froststrap %u";
    icon = "froststrap";
    categories = [ "Game" ];
    mimeTypes = [
      "x-scheme-handler/roblox"
      "x-scheme-handler/roblox-player"
      "x-scheme-handler/roblox-studio"
      "x-scheme-handler/roblox-studio-auth"
    ];
  };
in
appimageTools.wrapType2 {
  pname = "froststrap";
  inherit version src;

  extraPkgs = pkgs: [ pkgs.icu ];

  extraInstallCommands = ''
    install -Dm644 ${desktopItem}/share/applications/froststrap.desktop \
        $out/share/applications/froststrap.desktop

    install -Dm644 ${appimageContents}/usr/share/icons/hicolor/512x512/apps/froststrap.png \
        $out/share/icons/hicolor/512x512/apps/froststrap.png
  '';

  meta = {
    description = "A cross-platform Roblox bootstrapper, focused on performance and customization.";
    homepage = "https://froststrap.xyz/";
    license = lib.licenses.mpl20;
    platforms = [ "x86_64-linux" ];
  };
}
