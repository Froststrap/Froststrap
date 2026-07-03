param (
    [string]$Project = "Froststrap/Froststrap.csproj",
    [string]$BuildDir = "build",
    [string]$Config = "Release"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path "./$BuildDir")) {
    New-Item -ItemType Directory -Path "./$BuildDir" | Out-Null
}

$TempPublish = "./$BuildDir/temp-contained"
$Version = (git describe --tags --abbrev=0).TrimStart('v')

dotnet publish "$Project" /p:PublishProfile=Publish-contained-x64 -c "$Config" -o "$TempPublish" --configfile "$PSScriptRoot\..\..\nuget.config"

& makensis /DPUBLISH_DIR="..\$TempPublish" /DAPP_VERSION="$Version" /DSELFCONTAINED=1 Scripts/Installer.nsi

Remove-Item -Recurse -Force "$TempPublish"

Write-Host "Self-contained Windows installer complete: $BuildDir/Froststrap-SelfContained-Setup.exe" -ForegroundColor Green