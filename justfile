set windows-shell := ["powershell.exe", "-NoProfile", "-Command"]

project_file := "Froststrap/Froststrap.csproj"
build_dir := "build"
release_config := "Release"

# Git Helpers
# Returns the latest git tag or fails if none exists

git-version := `git describe --tags --abbrev=0 2>/dev/null || echo unknown`
git-commit-hash := `git rev-parse --short HEAD 2>/dev/null || echo unknown`
git-commit-ref := `git symbolic-ref --short HEAD 2>/dev/null || git rev-parse --short HEAD 2>/dev/null || echo unknown`

# Build

build:
    dotnet build -c {{ release_config }} --no-restore

# clean build artifacts and dotnet temp files
clean:
    @echo "Cleaning build artifacts..."
    {{ if os() == "windows" { "if (Test-Path " + build_dir + ") { Remove-Item -Recurse -Force " + build_dir + " }; " + "if (Test-Path ./Froststrap/bin) { Remove-Item -Recurse -Force ./Froststrap/bin }; " + "if (Test-Path ./Froststrap/obj) { Remove-Item -Recurse -Force ./Froststrap/obj }" } else { "rm -rf " + build_dir + " ./Froststrap/bin ./Froststrap/obj" } }}

# Internal validation helper
@_validate-publish commit_hash commit_ref:
    @echo "Preparing Froststrap Release"
    @echo "  Commit: {{ commit_hash }} ({{ commit_ref }})"
    @if [ -z "{{ commit_hash }}" ] || [ "{{ commit_hash }}" = "unknown" ]; then echo "ERROR: Invalid commit_hash"; exit 1; fi

# Windows Release
[windows]
publish-windows commit_hash commit_ref: (_validate-publish commit_hash commit_ref)
    @echo "Building Windows x64 Installer..."
    if (Test-Path {{ build_dir }}) { Remove-Item -Recurse -Force {{ build_dir }} }
    New-Item -ItemType Directory -Path {{ build_dir }} -Force | Out-Null

    dotnet publish {{ project_file }} `
        -r win-x64 `
        -c {{ release_config }} `
        --self-contained false `
        -p:PublishSingleFile=true `
        "/p:CommitHash={{ commit_hash }}" `
        "/p:CommitRef={{ commit_ref }}" `
        -o ./Froststrap/bin/{{ release_config }}/net10.0/publish

    $publishPath = "./Froststrap/bin/{{ release_config }}/net10.0/publish/Froststrap.exe"
    if (-not (Test-Path $publishPath)) { throw "Binary not found" }
    Copy-Item $publishPath ./{{ build_dir }}/

    $version = (git describe --tags --abbrev=0 2>$null) || "unknown"
    & makensis /DPUBLISH_DIR="{{ build_dir }}" /DAPP_VERSION="$version" Scripts/Installer.nsi

    Move-Item "./{{ build_dir }}/Froststrap-Setup.exe" "./{{ build_dir }}/Froststrap-$version.exe" -Force
    Remove-Item "./{{ build_dir }}/Froststrap.exe"
    @echo "Windows build complete: ./{{ build_dir }}/Froststrap-$version.exe"

# MacOS Release
[unix]
publish-macos commit_hash commit_ref: (_validate-publish commit_hash commit_ref)
    @echo "Building macOS ARM64 App Bundle..."
    rm -rf {{ build_dir }}
    mkdir -p {{ build_dir }}/Froststrap.app/Contents/{MacOS,Resources}

    dotnet publish {{ project_file }} \
        -r osx-arm64 \
        -c {{ release_config }} \
        --self-contained false \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        "-p:CommitHash={{ commit_hash }}" \
        "-p:CommitRef={{ commit_ref }}" \
        -o ./{{ build_dir }}/Froststrap.app/Contents/MacOS

    cp Info.plist ./{{ build_dir }}/Froststrap.app/Contents/Info.plist
    chmod +x ./{{ build_dir }}/Froststrap.app/Contents/MacOS/Froststrap

    version=$(git describe --tags --abbrev=0)
    (cd {{ build_dir }} && zip -r "Froststrap-macOS-arm64-$version.zip" Froststrap.app)
    @echo "MacOS build complete: ./{{ build_dir }}/Froststrap-macOS-arm64-$version.zip"

# Linux Release
[unix]
publish-linux commit_hash commit_ref: (_validate-publish commit_hash commit_ref)
    @echo "Building Linux x64 Binary..."
    rm -rf {{ build_dir }} && mkdir -p {{ build_dir }}

    dotnet publish {{ project_file }} \
        -r linux-x64 \
        -c {{ release_config }} \
        --self-contained false \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        "-p:CommitHash={{ commit_hash }}" \
        "-p:CommitRef={{ commit_ref }}" \
        -o ./{{ build_dir }}/linux-temp

    version=$(git describe --tags --abbrev=0 2>/dev/null || echo unknown)
    mv ./{{ build_dir }}/linux-temp/Froststrap ./{{ build_dir }}/Froststrap-linux-x64-$version
    rm -rf ./{{ build_dir }}/linux-temp
    chmod +x ./{{ build_dir }}/Froststrap-linux-x64-$version
    @echo "Linux build complete: ./{{ build_dir }}/Froststrap-linux-x64-$version"

# CI Actions
ci-publish-windows:
    @just publish-windows {{ git-commit-hash }} {{ git-commit-ref }}

ci-publish-macos:
    @just publish-macos {{ git-commit-hash }} {{ git-commit-ref }}

ci-publish-linux:
    @just publish-linux {{ git-commit-hash }} {{ git-commit-ref }}

# Debug Commands
debug-windows commit_hash commit_ref:
    dotnet publish {{ project_file }} -r win-x64 -c Debug --self-contained false -p:PublishSingleFile=true "/p:CommitHash={{ commit_hash }}" "/p:CommitRef={{ commit_ref }}"

[unix]
debug-macos commit_hash commit_ref:
    dotnet publish {{ project_file }} -r osx-arm64 -c Debug --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true "-p:CommitHash={{ commit_hash }}" "-p:CommitRef={{ commit_ref }}"

[unix]
debug-linux commit_hash commit_ref:
    dotnet publish {{ project_file }} -r linux-x64 -c Debug --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true "-p:CommitHash={{ commit_hash }}" "-p:CommitRef={{ commit_ref }}"

info:
    @echo "Build Information"
    @echo "  Project:     {{ project_file }}"
    @echo "  Config:      {{ release_config }}"
    @echo "  Git Version: {{ git-version }}"
    @echo "  Commit:      {{ git-commit-hash }} ({{ git-commit-ref }})"
