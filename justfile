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

# MacOS Release
[unix]
publish-macos:
    rm -rf {{ build_dir }}
    mkdir -p {{ build_dir }}/Froststrap.app/Contents/{MacOS,Resources}

    dotnet publish {{ project_file }} \
        -r osx-arm64 \
        -c {{ release_config }} \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o ./{{ build_dir }}/Froststrap.app/Contents/MacOS

    cp Info.plist ./{{ build_dir }}/Froststrap.app/Contents/Info.plist
    chmod +x ./{{ build_dir }}/Froststrap.app/Contents/MacOS/Froststrap
    hdiutil create -volname "Froststrap" -srcfolder ./{{ build_dir }}/Froststrap.app -ov -format UDZO ./{{ build_dir }}/Froststrap-macOS-arm64.dmg
    rm -rf ./{{ build_dir }}/Froststrap.app

# Linux Release
[unix]
publish-linux:
    rm -rf {{ build_dir }} && mkdir -p {{ build_dir }}

    dotnet publish {{ project_file }} \
        -r linux-x64 \
        -c {{ release_config }} \
        --self-contained false \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o ./{{ build_dir }}/linux-temp

    mv ./{{ build_dir }}/linux-temp/Froststrap ./{{ build_dir }}/Froststrap-linux-x64
    rm -rf ./{{ build_dir }}/linux-temp
    chmod +x ./{{ build_dir }}/Froststrap-linux-x64

# CI Actions
ci-publish-windows:
    @just publish-windows

ci-publish-macos:
    @just publish-macos

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
