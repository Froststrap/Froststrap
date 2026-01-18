set windows-shell := ["powershell.exe", "-c"]

build:
    dotnet build -c Release --no-restore

publish:
    if (Test-Path -path ./build) { rm -r build }
    mkdir build
    dotnet publish ./Froststrap.AvaloniaUI/Froststrap.AvaloniaUI.csproj /p:PublishProfile=Publish-x64
    cp ./Froststrap.AvaloniaUI/bin/Release/net10.0-windows/publish/win-x64/Froststrap.exe ./build/

clean:
    rm -r obj bin build
