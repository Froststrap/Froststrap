set windows-shell := ["powershell.exe", "-NoProfile", "-Command"]

project_file := "Froststrap/Froststrap.csproj"
build_dir := "build"
release_config := "Release"

# Build
build:
    dotnet build -c {{ release_config }} --no-restore

clean:
    @echo "Cleaning build artifacts..."
    {{ if os() == "windows" { "if (Test-Path " + build_dir + ") { Remove-Item -Recurse -Force " + build_dir + " }; " + "if (Test-Path ./Froststrap/bin) { Remove-Item -Recurse -Force ./Froststrap/bin }; " + "if (Test-Path ./Froststrap/obj) { Remove-Item -Recurse -Force ./Froststrap/obj }" } else { "rm -rf " + build_dir + " ./Froststrap/bin ./Froststrap/obj" } }}

# Windows Release
[windows]
publish-windows:
    if (Test-Path -Path ./{{ build_dir }}) { rm -r {{ build_dir }} }
    mkdir {{ build_dir }}
    dotnet publish ./{{ project_file }} /p:PublishProfile=Publish-x64
    cp ./Froststrap/bin/{{ release_config }}/net10.0/publish/Froststrap.exe ./{{ build_dir }}/
    $version = (git describe --tags --abbrev=0); \
    & makensis /DPUBLISH_DIR="..\{{ build_dir }}" /DAPP_VERSION="$version" Scripts/Installer.nsi
    mv ./{{ build_dir }}/Froststrap-Setup.exe "./{{ build_dir }}/Froststrap-Setup.exe"
    rm ./{{ build_dir }}/Froststrap.exe

# macOS Release
[unix]
publish-macos:
    rm -rf {{ build_dir }}
    mkdir -p {{ build_dir }}/temp/arm64
    mkdir -p {{ build_dir }}/temp/x64
    mkdir -p {{ build_dir }}/Froststrap.app/Contents/{MacOS,Resources}

    dotnet publish {{ project_file }} \
        -r osx-arm64 \
        -c {{ release_config }} \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o ./{{ build_dir }}/temp/arm64

    dotnet publish {{ project_file }} \
        -r osx-x64 \
        -c {{ release_config }} \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o ./{{ build_dir }}/temp/x64

# Linux Release
[unix]
publish-linux:
    rm -rf {{ build_dir }} && mkdir -p {{ build_dir }}

    dotnet publish {{ project_file }} \
        -r linux-x64 \
        -c {{ release_config }} \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o ./{{ build_dir }}/linux-temp

    mv ./{{ build_dir }}/linux-temp/Froststrap ./{{ build_dir }}/Froststrap-linux-x64
    rm -rf ./{{ build_dir }}/linux-temp
    chmod +x ./{{ build_dir }}/Froststrap-linux-x64

    rm -rf ./{{ build_dir }}/AppDir
    mkdir -p ./{{ build_dir }}/AppDir/usr/bin
    mkdir -p ./{{ build_dir }}/AppDir/usr/share/applications
    mkdir -p ./{{ build_dir }}/AppDir/usr/share/icons/hicolor/512x512/apps
    cp ./{{ build_dir }}/Froststrap-linux-x64 ./{{ build_dir }}/AppDir/usr/bin/Froststrap
    cp ./Froststrap/Froststrap.png ./{{ build_dir }}/AppDir/froststrap.png
    cp ./Froststrap/Froststrap.png ./{{ build_dir }}/AppDir/usr/share/icons/hicolor/512x512/apps/froststrap.png

    printf '%s\n' \
        '#!/bin/sh' \
        'HERE="$(dirname "$(readlink -f "$0")")"' \
        'exec "$HERE/usr/bin/Froststrap" "$@"' \
        > ./{{ build_dir }}/AppDir/AppRun
    chmod +x ./{{ build_dir }}/AppDir/AppRun

    version="$(git describe --tags --always --dirty 2>/dev/null || echo dev)"; \
    printf '%s\n' \
        '[Desktop Entry]' \
        'Type=Application' \
        'Name=Froststrap' \
        'Comment=Roblox bootstrapper and mod manager' \
        'Exec=Froststrap %u' \
        'TryExec=Froststrap' \
        'Icon=froststrap' \
        'Terminal=false' \
        'Categories=Game;' \
        'MimeType=x-scheme-handler/roblox;x-scheme-handler/roblox-player;' \
        "X-AppImage-Version=$version" \
        > ./{{ build_dir }}/AppDir/Froststrap.desktop
    cp ./{{ build_dir }}/AppDir/Froststrap.desktop ./{{ build_dir }}/AppDir/usr/share/applications/Froststrap.desktop

    if command -v appimagetool >/dev/null 2>&1; then \
        APPIMAGE_TOOL=appimagetool; \
    else \
        curl -L --fail -o ./{{ build_dir }}/appimagetool.AppImage \
            https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage && \
        chmod +x ./{{ build_dir }}/appimagetool.AppImage && \
        APPIMAGE_TOOL=./{{ build_dir }}/appimagetool.AppImage; \
    fi; \
    env -u SOURCE_DATE_EPOCH ARCH=x86_64 "$APPIMAGE_TOOL" --appimage-extract-and-run \
        ./{{ build_dir }}/AppDir \
        ./{{ build_dir }}/Froststrap-linux-x64.AppImage

# CI Actions
ci-publish-windows:
    @just publish-windows

ci-publish-macos:
    @just publish-macos
    lipo -create \
        ./{{ build_dir }}/temp/x64/Froststrap \
        ./{{ build_dir }}/temp/arm64/Froststrap \
        -output ./{{ build_dir }}/Froststrap.app/Contents/MacOS/Froststrap

    cp ./macos/Info.plist ./{{ build_dir }}/Froststrap.app/Contents/Info.plist
    chmod +x ./{{ build_dir }}/Froststrap.app/Contents/MacOS/Froststrap

    # Ad-hoc code sign the app (self-signed)
    codesign --force --deep --sign - ./{{ build_dir }}/Froststrap.app

    # use create-dmg to make gui
    create-dmg \
      --volname "Froststrap Installer" \
      --window-size 500 300 \
      --icon-size 96 \
      --icon "Froststrap.app" 125 150 \
      --app-drop-link 375 150 \
      "./{{ build_dir }}/Froststrap-macOS.dmg" \
      "./{{ build_dir }}/Froststrap.app"

    # Clean up
    rm -rf ./{{ build_dir }}/temp
    rm -rf ./{{ build_dir }}/Froststrap.app

ci-publish-linux:
    @just publish-linux

# Debug Commands
debug-windows:
    dotnet publish {{ project_file }} -r win-x64 -c Debug --self-contained true -p:PublishSingleFile=true

[unix]
debug-macos:
    dotnet publish {{ project_file }} -r osx-arm64 -c Debug --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

[unix]
debug-linux:
    dotnet publish {{ project_file }} -r linux-x64 -c Debug --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

info:
    @echo "Build Information"
    @echo "  Project:     {{ project_file }}"
    @echo "  Config:      {{ release_config }}"
