project_file := "Froststrap/Froststrap.csproj"
build_dir := "build"
release_config := "Release"

build:
    dotnet build -c {{ release_config }} --no-restore

# Debug Commands
[windows]
debug-windows:
    dotnet publish {{ project_file }} -r win-x64 -c Debug --self-contained true -p:PublishSingleFile=true --configfile nuget.config

[unix]
debug-macos:
    dotnet publish {{ project_file }} -r osx-arm64 -c Debug --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --configfile nuget.config

[unix]
debug-linux:
    dotnet publish {{ project_file }} -r linux-x64 -c Debug --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --configfile nuget.config

[windows]
publish-windows:
    #!powershell
    powershell -ExecutionPolicy Bypass -File ./Scripts/package/publish-windows.ps1 -Project "{{ project_file }}" -BuildDir "{{ build_dir }}"

[unix]
publish-macos:
    chmod +x Scripts/package/publish-macos.sh
    ./Scripts/package/publish-macos.sh

[unix]
publish-linux:
    chmod +x ./Scripts/package/publish-linux.sh
    ./Scripts/package/publish-linux.sh "{{ project_file }}" "{{ build_dir }}" "Publish-linux-x64"

# CI Aliases
ci-publish-windows:
    @just publish-windows
    
ci-publish-macos:
    which -a ld
    which -a swiftc
    echo $PATH
    @just publish-macos

ci-publish-linux:
    @just publish-linux
