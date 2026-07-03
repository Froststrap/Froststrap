param (
    [string]$Project = "Froststrap/Froststrap.csproj",
    [string]$BuildDir = "build",
    [string]$Config = "Release"
)

$ErrorActionPreference = "Stop"

if (Test-Path -Path "./$BuildDir") { Remove-Item -Recurse -Force "./$BuildDir" }
New-Item -ItemType Directory -Path "./$BuildDir" | Out-Null

dotnet publish "$Project" /p:PublishProfile=Publish-x64 -c "$Config" --configfile "$PSScriptRoot\..\..\nuget.config"

$PublishPath = "./Froststrap/bin/$Config/net10.0/publish/Froststrap.exe"
Copy-Item $PublishPath -Destination "./$BuildDir/"

$Version = (git describe --tags --abbrev=0).TrimStart('v')
& makensis /DPUBLISH_DIR="..\$BuildDir" /DAPP_VERSION="$Version" Scripts/Installer.nsi

Remove-Item "./$BuildDir/Froststrap.exe"

Write-Host "Windows build complete: $BuildDir/Froststrap-Setup.exe" -ForegroundColor Green