param(
    [string]$SourceFile = "Froststrap/Resources/Strings.resx",
    [string]$ApiEndpoint = "https://libretranslate.com/translate",
    [string[]]$TargetLocales = @(
        "ar", "bg", "cs", "de", "es-ES", "fa", "fi", "fil", "fr",
        "hr", "hu", "id", "it", "ja", "ko", "lt", "ms", "nl", "pl",
        "pt-BR", "ro", "ru", "sv-SE", "th", "tr", "uk", "vi", "zh-CN", "zh-TW"
    )
)

Add-Type -AssemblyName System.Resources.Reader
Add-Type -AssemblyName System.Resources.Writer

function Invoke-LibreTranslate {
    param(
        [string]$Text,
        [string]$TargetLang
    )
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Text }

    $body = @{
        q      = $Text
        source = "en"
        target = $TargetLang
        format = "text"
    } | ConvertTo-Json

    try {
        Start-Sleep -Milliseconds 3000
        $response = Invoke-RestMethod -Uri $ApiEndpoint -Method Post -Body $body -ContentType "application/json"
        return $response.translatedText
    } catch {
        Write-Warning "Translation failed for '$Text' to $TargetLang : $_"
        return $Text
    }
}

Write-Host "Reading source file: $SourceFile"
$sourceReader = [System.Resources.ResXResourceReader]::new($SourceFile)
$sourceReader.UseResXDataNodes = $true
$strings = @{}
foreach ($entry in $sourceReader) {
    $node = $entry.Value -as [System.Resources.ResXDataNode]
    if ($node -and $node.FileRef -eq $null) {
        $value = $node.GetValue(([System.ComponentModel.Design.ITypeResolutionService]$null)) -as [string]
        $strings[$node.Name] = $value
    }
}
$sourceReader.Close()
Write-Host "Loaded $($strings.Count) string entries."

foreach ($locale in $TargetLocales) {
    $targetFile = "Froststrap/Resources/Strings.$locale.resx"
    Write-Host "Translating to '$locale' -> $targetFile"

    $writer = [System.Resources.ResXResourceWriter]::new($targetFile)
    $translatedCount = 0
    $failedCount = 0

    foreach ($name in $strings.Keys) {
        $original = $strings[$name]
        if ($original) {
            $translated = Invoke-LibreTranslate -Text $original -TargetLang $locale
            if ($translated -eq $original) { $failedCount++ }
            $writer.AddResource($name, $translated)
            $translatedCount++
        } else {
            $writer.AddResource($name, "")
        }
    }
    $writer.Close()
    Write-Host "Saved $targetFile (Translated: $translatedCount, Failed: $failedCount)"
}

Write-Host "All translations completed."