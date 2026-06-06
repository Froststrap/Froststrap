param (
    [string]$Project = "Froststrap/Froststrap.csproj",
    [string]$BuildDir = "build",
    [string]$Config = "Release"
)

$ErrorActionPreference = "Stop"

if (Test-Path -Path "./$BuildDir") { Remove-Item -Recurse -Force "./$BuildDir" }
New-Item -ItemType Directory -Path "./$BuildDir" | Out-Null

dotnet publish "$Project" /p:PublishProfile=Publish-contained-x64 -c "$Config"

$PublishPath = "./Froststrap/bin/$Config/net10.0/publish/Froststrap.exe"
Copy-Item $PublishPath -Destination "./$BuildDir/Froststrap-SelfContained-Setup.exe"

Write-Host "Self-contained Windows build complete: $BuildDir/Froststrap-contained.exe" -ForegroundColor Green