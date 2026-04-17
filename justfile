set windows-shell := ["powershell.exe", "-c"]

build:
    dotnet build -c Release --no-restore

publish-windows commit_hash commit_ref:
    if (Test-Path -path ./build) { rm -r build }
    mkdir build
    dotnet publish ./Froststrap/Froststrap.csproj /p:PublishProfile=Publish-x64 "-p:CommitHash={{commit_hash}}" "-p:CommitRef={{commit_ref}}"
    cp ./Froststrap/bin/Release/net10.0/publish/Froststrap.exe ./build/
    $version = (git describe --tags --abbrev=0); \
    makensis /DPUBLISH_DIR="..\build" /DAPP_VERSION="$version" Scripts\Installer.nsi; \
    mv ./build/Froststrap-Setup.exe "./build/Froststrap-$version.exe"
    rm ./build/Froststrap.exe

publish-macos commit_hash commit_ref:
    rm -rf build || true
    mkdir build
    dotnet publish ./Froststrap/Froststrap.csproj -p:PublishSingleFile=true "-p:CommitHash={{commit_hash}}" "-p:CommitRef={{commit_ref}}" -r osx-arm64 -c Release --self-contained false
    mv ./Froststrap/bin/Release/net10.0/osx-arm64/publish/Froststrap ./build/Froststrap-macOS-arm64

publish-linux commit_hash commit_ref:
    rm -rf build || true
    mkdir build
    dotnet publish ./Froststrap/Froststrap.csproj -p:PublishSingleFile=true "-p:CommitHash={{commit_hash}}" "-p:CommitRef={{commit_ref}}" -r linux-x64 -c Release --self-contained false
    mv ./Froststrap/bin/Release/net10.0/linux-x64/publish/Froststrap ./build/Froststrap-linux-x64

debug-windows commit_hash commit_ref:
    dotnet publish Froststrap/Froststrap.csproj -p:PublishSingleFile=true "-p:CommitHash={{commit_hash}}" "-p:CommitRef={{commit_ref}}" -r win-x64 -c Debug --self-contained false

debug-macos commit_hash commit_ref:
    dotnet publish Froststrap/Froststrap.csproj -p:PublishSingleFile=true "-p:CommitHash={{commit_hash}}" "-p:CommitRef={{commit_ref}}" -r osx-arm64 -c Debug --self-contained false

debug-linux commit_hash commit_ref:
    dotnet publish Froststrap/Froststrap.csproj -p:PublishSingleFile=true "-p:CommitHash={{commit_hash}}" "-p:CommitRef={{commit_ref}}" -r linux-x64 -c Debug --self-contained false

clean:
    rm -r obj bin build
