{
  lib,
  appimageTools,
  fetchurl,
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
in
appimageTools.wrapType2 {
  pname = "froststrap";
  inherit version src;

  extraPkgs = pkgs: [
    pkgs.icu
  ];

  extraInstallCommands = ''
    install -Dm644 ${appimageContents}/usr/share/applications/Froststrap.desktop \
        $out/share/applications/Froststrap.desktop

    install -Dm644 ${appimageContents}/usr/share/icons/hicolor/512x512/apps/froststrap.png \
        $out/share/icons/hicolor/512x512/apps/froststrap.png
  '';

  meta = {
    description = "A cross-platform Roblox bootstrapper, focused on performance and customization.";
    homepage = "https://froststrap.xyz/";
    license = lib.licenses.mpl20;
    platforms = [ "x86_64-linux" ];
    mainProgram = "froststrap";
  };
}
