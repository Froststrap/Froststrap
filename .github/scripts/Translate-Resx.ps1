param(
    [string]$SourceFile = "Froststrap/Resources/Strings.resx",
    [string]$ApiEndpoint = "https://libretranslatelibretranslate-production-4abc.up.railway.app/translate",
    [string]$ApiKey = $env:LT_API_KEY,
    [string[]]$TargetLocales = @(
        "ar", "bg", "cs", "de", "es-ES", "fa", "fi", "fil", "fr",
        "hr", "hu", "id", "it", "ja", "ko", "lt", "ms", "nl", "pl",
        "pt-BR", "ro", "ru", "sv-SE", "th", "tr", "uk", "vi", "zh-CN", "zh-TW"
    )
)

function Invoke-LibreTranslate {
    param([string]$Text, [string]$TargetLang)
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Text }
    $body = @{
        q = $Text
        source = "en"
        target = $TargetLang
        format = "text"
        api_key = $ApiKey
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

function Read-Resx {
    param([string]$Path)
    $xml = New-Object System.Xml.XmlDocument
    $xml.Load($Path)
    $strings = @{}
    foreach ($data in $xml.SelectNodes("//data")) {
        $name = $data.GetAttribute("name")
        $valueNode = $data.SelectSingleNode("value")
        if ($valueNode -ne $null) {
            $strings[$name] = $valueNode.InnerText
        }
    }
    return $strings
}

function Write-Resx {
    param([string]$Path, [hashtable]$Strings)
    $xml = New-Object System.Xml.XmlDocument
    $root = $xml.CreateElement("root")
    $xml.AppendChild($root) | Out-Null
    $resheader = @(
        @{ name = "resmimetype"; value = "text/microsoft-resx" },
        @{ name = "version"; value = "2.0" },
        @{ name = "reader"; value = "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" },
        @{ name = "writer"; value = "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" }
    )
    foreach ($h in $resheader) {
        $header = $xml.CreateElement("resheader")
        $header.SetAttribute("name", $h.name)
        $value = $xml.CreateElement("value")
        $value.InnerText = $h.value
        $header.AppendChild($value) | Out-Null
        $root.AppendChild($header) | Out-Null
    }
    foreach ($name in $Strings.Keys) {
        $data = $xml.CreateElement("data")
        $data.SetAttribute("name", $name)
        $data.SetAttribute("xml:space", "preserve")
        $value = $xml.CreateElement("value")
        $value.InnerText = $Strings[$name]
        $data.AppendChild($value) | Out-Null
        $root.AppendChild($data) | Out-Null
    }
    $xml.Save($Path)
}

Write-Host "Reading source file: $SourceFile"
$strings = Read-Resx -Path $SourceFile
Write-Host "Loaded $($strings.Count) string entries."

foreach ($locale in $TargetLocales) {
    $targetFile = "Froststrap/Resources/Strings.$locale.resx"
    Write-Host "Translating to '$locale' -> $targetFile"
    $translated = @{}
    $failed = 0
    foreach ($name in $strings.Keys) {
        $original = $strings[$name]
        if ($original) {
            $translatedText = Invoke-LibreTranslate -Text $original -TargetLang $locale
            if ($translatedText -eq $original) { $failed++ }
            $translated[$name] = $translatedText
        } else {
            $translated[$name] = ""
        }
    }
    Write-Resx -Path $targetFile -Strings $translated
    Write-Host "Saved $targetFile (Failed: $failed)"
}

Write-Host "All translations completed."