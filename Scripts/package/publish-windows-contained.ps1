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
dotnet publish "$Project" /p:PublishProfile=Publish-contained-x64 -c "$Config" -o "$TempPublish"

Copy-Item "$TempPublish/Froststrap.exe" -Destination "./$BuildDir/Froststrap-SelfContained-Setup.exe"

Remove-Item -Recurse -Force "$TempPublish"

Write-Host "Self-contained Windows build complete: $BuildDir/Froststrap-SelfContained-Setup.exe" -ForegroundColor Green