set windows-shell := ["powershell.exe", "-c"]

build:
    dotnet build -c Release --no-restore

publish:
    if (Test-Path -path ./build) { rm -r build }
    mkdir build
    dotnet publish ./Froststrap/Froststrap.csproj /p:PublishProfile=Publish-x64
    cp ./Froststrap/bin/Release/net10.0/publish/Froststrap.exe ./build/
    $version = (git describe --tags --abbrev=0); \
    makensis /DPUBLISH_DIR="..\build" /DAPP_VERSION="$version" Scripts\Installer.nsi; \
    mv ./build/Froststrap-Setup.exe "./build/Froststrap-v$version.exe"
    rm ./build/Froststrap.exe

clean:
    rm -r obj bin build
