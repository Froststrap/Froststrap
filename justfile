project_file := "Froststrap/Froststrap.csproj"
build_dir := "build"
release_config := "Release"

build:
    dotnet build -c {{ release_config }} --no-restore

# Debug Commands
[windows]
debug-windows:
    dotnet publish {{ project_file }} -r win-x64 -c Debug --self-contained true -p:PublishSingleFile=true

[unix]
debug-macos:
    dotnet publish {{ project_file }} -r osx-arm64 -c Debug --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

[unix]
debug-linux:
    dotnet publish {{ project_file }} -r linux-x64 -c Debug --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

[windows]
publish-windows:
    #!powershell
    pwsh ./Scripts/package/publish-windows.ps1 -Project "{{ project_file }}" -BuildDir "{{ build_dir }}"

[unix]
publish-macos:
    chmod +x ./Scripts/package/publish-macos.sh
    ./Scripts/package/publish-macos.sh "{{ project_file }}" "{{ build_dir }}" "Publish-osx-arm64" "Publish-osx-x64"

[unix]
publish-linux:
    chmod +x ./Scripts/package/publish-linux.sh
    ./Scripts/package/publish-linux.sh "{{ project_file }}" "{{ build_dir }}" "Publish-linux-x64"

[unix]
publish-flatpak:
    chmod +x ./Scripts/package/publish-flatpak.sh
    ./Scripts/package/publish-flatpak.sh "{{ project_file }}" "{{ build_dir }}"

# CI Aliases
ci-publish-windows:
    @just publish-windows

ci-publish-macos:
    @just publish-macos

ci-publish-linux:
    @just publish-linux

ci-publish-flatpak:
    @just publish-flatpak
