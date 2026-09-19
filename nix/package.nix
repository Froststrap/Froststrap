{
  lib,
  appimageTools,
  fetchurl,
}:

let
  version = "2.0.0";

  src = fetchurl {
    url = "https://github.com/Froststrap/Froststrap/releases/download/v${version}/Froststrap-linux-x64.AppImage";
    hash = "sha256-zq/SV9PjeZ28MHJkbvWlzqtiKV6BmyF8LKQdSAkEbXY=";
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

    substituteInPlace $out/share/applications/Froststrap.desktop \
        --replace-fail "Exec=Froststrap %u" "Exec=froststrap %u" \
        --replace-fail "TryExec=Froststrap" "TryExec=froststrap"
  '';

  meta = {
    description = "A cross-platform Roblox bootstrapper, focused on performance and customization.";
    homepage = "https://github.com/Froststrap/Froststrap";
    license = lib.licenses.mpl20;
    platforms = [ "x86_64-linux" ];
    mainProgram = "froststrap";
  };
}
