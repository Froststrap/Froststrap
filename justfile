project_file := "Froststrap/Froststrap.csproj"
build_dir := "build"
release_config := "Release"

build:
    dotnet build -c {{ release_config }} --no-restore

[windows]
publish-windows:
    pwsh ./Scripts/package/publish-windows.ps1 -Project "{{ project_file }}" -BuildDir "{{ build_dir }}"

[unix]
publish-macos:
    chmod +x ./Scripts/package/publish-macos.sh
    ./Scripts/package/publish-macos.sh "{{ project_file }}" "{{ build_dir }}"

[unix]
publish-linux:
    chmod +x ./Scripts/package/publish-linux.sh
    ./Scripts/package/publish-linux.sh "{{ project_file }}" "{{ build_dir }}"

# CI Aliases
ci-publish-windows:
    @just publish-windows

ci-publish-macos:
    @just publish-macos

ci-publish-linux:
    @just publish-linux
