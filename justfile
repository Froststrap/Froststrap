set windows-shell := ["powershell.exe", "-NoProfile", "-Command"]

project_file := "Froststrap/Froststrap.csproj"
build_dir := "build"
release_config := "Release"

# Git Helpers

git-commit-hash := `git rev-parse --short HEAD 2>/dev/null || echo unknown`
git-commit-ref := `git symbolic-ref --short HEAD 2>/dev/null || git rev-parse --short HEAD 2>/dev/null || echo unknown`

# Build

build:
    dotnet build -c {{ release_config }} --no-restore

clean:
    @echo "Cleaning build artifacts..."
    {{ if os() == "windows" { "if (Test-Path " + build_dir + ") { Remove-Item -Recurse -Force " + build_dir + " }; " + "if (Test-Path ./Froststrap/bin) { Remove-Item -Recurse -Force ./Froststrap/bin }; " + "if (Test-Path ./Froststrap/obj) { Remove-Item -Recurse -Force ./Froststrap/obj }" } else { "rm -rf " + build_dir + " ./Froststrap/bin ./Froststrap/obj" } }}

# Windows Release
[windows]
publish-windows:
    if (Test-Path {{ build_dir }}) { Remove-Item -Recurse -Force {{ build_dir }} }
    New-Item -ItemType Directory -Path {{ build_dir }} -Force | Out-Null

    dotnet publish {{ project_file }} `
        -r win-x64 `
        -c {{ release_config }} `
        --self-contained false `
        -p:PublishSingleFile=true `
        -o ./Froststrap/bin/{{ release_config }}/net10.0/publish

    $publishPath = "./Froststrap/bin/{{ release_config }}/net10.0/publish/Froststrap.exe"
    if (-not (Test-Path $publishPath)) { throw "Binary not found" }
    Copy-Item $publishPath ./{{ build_dir }}/Froststrap.exe
    & makensis /DPUBLISH_DIR="{{ build_dir }}" Scripts/Installer.nsi
    Move-Item "./{{ build_dir }}/Froststrap-Setup.exe" "./{{ build_dir }}/Froststrap.exe" -Force

# MacOS Release
[unix]
publish-macos:
    rm -rf {{ build_dir }}
    mkdir -p {{ build_dir }}/Froststrap.app/Contents/{MacOS,Resources}

    dotnet publish {{ project_file }} \
        -r osx-arm64 \
        -c {{ release_config }} \
        --self-contained false \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o ./{{ build_dir }}/Froststrap.app/Contents/MacOS

    cp Info.plist ./{{ build_dir }}/Froststrap.app/Contents/Info.plist
    chmod +x ./{{ build_dir }}/Froststrap.app/Contents/MacOS/Froststrap
    (cd {{ build_dir }} && zip -r "Froststrap-macOS-arm64.zip" Froststrap.app)

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
    dotnet publish {{ project_file }} -r win-x64 -c Debug --self-contained false -p:PublishSingleFile=true

[unix]
debug-macos:
    dotnet publish {{ project_file }} -r osx-arm64 -c Debug --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

[unix]
debug-linux:
    dotnet publish {{ project_file }} -r linux-x64 -c Debug --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

info:
    @echo "Build Information"
    @echo "  Project:     {{ project_file }}"
    @echo "  Config:      {{ release_config }}"
    @echo "  Commit:      {{ git-commit-hash }} ({{ git-commit-ref }})"
