set windows-shell := ["powershell.exe", "-c"]

build:
    dotnet build -c Release --no-restore

publish:
    if (Test-Path -path ./build) { rm -r build }
    mkdir build
    dotnet publish ./Froststrap/Froststrap.csproj /p:PublishProfile=Publish-x64
    cp ./Froststrap/bin/Release/net10.0/publish/Froststrap.exe ./build/
    makensis /DPUBLISH_DIR="..\build" Scripts\Installer.nsi

installer:
    dotnet publish ./Froststrap/Froststrap.csproj -c Release

clean:
    rm -r obj bin build
