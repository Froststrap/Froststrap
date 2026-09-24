// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Enums.GBSPresets;
using System.Globalization;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Froststrap;

internal class GBSEditor
{
    public XDocument? Document { get; set; } = null!;

    public Dictionary<string, string> PresetPaths = new()
    {
        // Graphics Settings
        { "Rendering.FramerateCap", "{UserSettings}/int[@name='FramerateCap']" },
        { "Rendering.SavedQualityLevel", "{UserSettings}/token[@name='SavedQualityLevel']" },
        { "Rendering.Fullscreen", "{UserSettings}/bool[@name='Fullscreen']" },
        { "Rendering.MaxQualityEnabled", "{UserSettings}/bool[@name='MaxQualityEnabled']" },
        { "Rendering.VignetteEnabled", "{UserSettings}/bool[@name='VignetteEnabled']" },
        { "Rendering.VignetteEnableOption", "{UserSettings}/bool[@name='VignetteEnabledCustomOption']" },

        // Audio Settings
        { "Audio.MasterVolume", "{UserSettings}/float[@name='MasterVolume']" },
        { "Audio.MasterVolumeStudio", "{UserSettings}/float[@name='MasterVolumeStudio']" },
        { "Audio.PartyVoiceVolume", "{UserSettings}/float[@name='PartyVoiceVolume']" },
        { "Audio.VoiceChatVolume", "{UserSettings}/float[@name='VoiceChatVolume']" },

        // Input Settings
        { "User.MouseSensitivity", "{UserSettings}/float[@name='MouseSensitivity']" },
        { "User.ShiftLock", "{UserSettings}/token[@name='ControlMode']" },
        { "User.MouseSensitivityFirstPerson", "{UserSettings}/Vector2[@name='MouseSensitivityFirstPerson']" },
        { "User.MouseSensitivityThirdPerson", "{UserSettings}/Vector2[@name='MouseSensitivityThirdPerson']" },
        { "User.CameraYInverted", "{UserSettings}/bool[@name='CameraYInverted']" },
        { "User.HapticStrength", "{UserSettings}/float[@name='HapticStrength']" },

        // Accessibility
        { "UI.Transparency", "{UserSettings}/float[@name='PreferredTransparency']" },
        { "UI.ReducedMotion", "{UserSettings}/bool[@name='ReducedMotion']" },
        { "UI.FontSize", "{UserSettings}/token[@name='PreferredTextSize']" },
        { "UI.PlayerListLayOut", "{UserSettings}/token[@name='PeoplePageLayout']" },

        // Miscellaneous Settings
        { "Misc.PerformanceStatsVisible", "{UserSettings}/bool[@name='PerformanceStatsVisible']" },
        { "Misc.ChatTranslationEnabled", "{UserSettings}/bool[@name='ChatTranslationEnabled']" },
        { "Misc.ChatTranslationFTUXShown", "{UserSettings}/bool[@name='ChatTranslationFTUXShown']" },
        { "User.VREnabled", "{UserSettings}/bool[@name='VREnabled']" }
    };

    // we are making it easier for ourselves
    // basically replacing {...} with a path
    // might expand in the future (studio support)
    public Dictionary<string, string> RootPaths = new()
    {
        { "UserSettings", "//Item[@class='UserGameSettings']/Properties" },
    };

    private static readonly HashSet<string> KnownTypes = new(StringComparer.Ordinal)
    {
        "bool", "int", "float", "token", "string", "Vector2"
    };

    private static readonly Regex PathTailRegex = new(
        @"^(?<parent>.+)/(?<type>[A-Za-z0-9_]+)\[@name='(?<name>[A-Za-z0-9_]+)'\]$",
        RegexOptions.Compiled);

    private static readonly Regex ValidSettingName = new(@"^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    public static IReadOnlyDictionary<FontSize, string?> FontSizes => new Dictionary<FontSize, string?>
    {
        { FontSize.x1, "1" },
        { FontSize.x2, "2" },
        { FontSize.x3, "3" },
        { FontSize.x4, "4" }
    };

    public static IReadOnlyDictionary<PlayerListLayOut, string?> PlayerListLayOuts => new Dictionary<PlayerListLayOut, string?>
    {
        { PlayerListLayOut.x0, "0" },
        { PlayerListLayOut.x1, "1" }
    };

    public bool Loaded { get; set; }

    public static string FileLocation => OperatingSystem.IsLinux() ?
            Path.Combine(Paths.SoberData, "appData", "GlobalBasicSettings_13.xml") :
                OperatingSystem.IsMacOS() ?
                    Path.Combine(Paths.UserProfile, "Library", "Roblox", "GlobalBasicSettings_13.xml") :
                        Path.Combine(Paths.Roblox, "GlobalBasicSettings_13.xml");

    private string? _savedHash;

    private string ComputeHash()
    {
        if (Document == null) return string.Empty;
        return FastHash.FromString(Document.ToString());
    }

    public bool HasUnsavedChanges
    {
        get
        {
            if (_savedHash == null) return false;
            return ComputeHash() != _savedHash;
        }
    }

    public void SetPreset(string prefix, object? value)
    {
        var matchingPaths = PresetPaths.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        if (matchingPaths.Count == 0)
        {
            App.Logger.Warn($"SetPreset: No preset paths found for prefix '{prefix}'");
            return;
        }

        foreach (var pair in matchingPaths)
            SetValue(pair.Value, value);
    }

    public string? GetPreset(string prefix)
    {
        if (PresetPaths.TryGetValue(prefix, out string? path))
            return GetValue(path);
        return null;
    }

    public void SetValue(string path, object? value)
    {
        if (Document is null)
        {
            App.Logger.Warn($"SetValue: No document loaded, cannot set '{path}'.");
            return;
        }

        path = ResolvePath(path, RootPaths);
        XElement? element = Document.XPathSelectElement(path);
        Match tail = PathTailRegex.Match(path);

        string? type = element?.Name.LocalName ?? (tail.Success ? tail.Groups["type"].Value : null);
        if (type is null)
        {
            App.Logger.Warn($"SetValue: Unsupported path '{path}'. Changes will not be applied.");
            return;
        }

        if (type == "Vector2" || element?.HasElements == true)
        {
            App.Logger.Warn($"SetValue: '{path}' is a compound element, use SetVectorValue. Skipping.");
            return;
        }

        if (!TryNormalizeValue(type, value, out string newValue))
        {
            App.Logger.Warn($"SetValue: Rejected value '{value}' for {type} at '{path}'.");
            return;
        }

        if (element is null)
        {
            element = CreateMissingElement(path);
            if (element is null)
            {
                App.Logger.Warn($"SetValue: Element not found or could not be created for path '{path}'. Changes will not be applied.");
                return;
            }
        }

        if (element.Value != newValue)
        {
            App.Logger.Debug($"SetValue: Changing '{path}' from '{element.Value}' to '{newValue}'");
            element.Value = newValue;
        }
    }

    private XElement? CreateMissingElement(string resolvedPath)
    {
        Match tail = PathTailRegex.Match(resolvedPath);
        if (!tail.Success)
            return null;

        string type = tail.Groups["type"].Value;
        string name = tail.Groups["name"].Value;

        if (!KnownTypes.Contains(type) || !ValidSettingName.IsMatch(name))
            return null;

        XElement? parent = Document?.XPathSelectElement(tail.Groups["parent"].Value);
        if (parent is null)
            return null;

        var element = new XElement(type, new XAttribute("name", name));

        if (type == "Vector2")
        {
            element.Add(new XElement("X", "0"), new XElement("Y", "0"));
        }

        parent.Add(element);
        App.Logger.Info($"SetValue: Created missing element '{type}' with name '{name}' at path '{resolvedPath}'");
        return element;
    }

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static bool TryNormalizeFloat(string raw, out string result)
    {
        result = string.Empty;
        raw = raw.Trim();

        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) &&
            !float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out f))
            return false;

        if (!float.IsFinite(f))
            return false;

        result = f.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TryNormalizeValue(string type, object? value, out string result)
    {
        result = string.Empty;
        string raw = FormatValue(value);

        switch (type)
        {
            case "bool":
                if (raw.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)) { result = "true"; return true; }
                if (raw.Trim().Equals("false", StringComparison.OrdinalIgnoreCase)) { result = "false"; return true; }
                return false;

            case "int":
            case "token":
                if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                {
                    result = i.ToString(CultureInfo.InvariantCulture);
                    return true;
                }
                if (decimal.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal d) &&
                    d == decimal.Truncate(d) && d >= int.MinValue && d <= int.MaxValue)
                {
                    result = ((int)d).ToString(CultureInfo.InvariantCulture);
                    return true;
                }
                return false;

            case "float":
                return TryNormalizeFloat(raw, out result);

            case "string":
                result = raw;
                return true;

            default:
                return false;
        }
    }

    public string? GetValue(string path)
    {
        path = ResolvePath(path, RootPaths);
        return Document?.XPathSelectElement(path)?.Value;
    }

    public bool previousReadOnlyState;

    public void SetReadOnly(bool readOnly, bool preserveState = false)
    {
        if (!File.Exists(FileLocation))
            return;

        try
        {
            FileAttributes attributes = File.GetAttributes(FileLocation);

            if (readOnly)
                attributes |= FileAttributes.ReadOnly;
            else
                attributes &= ~FileAttributes.ReadOnly;

            File.SetAttributes(FileLocation, attributes);

            if (!preserveState)
                previousReadOnlyState = readOnly;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to set read-only on {FileLocation}", ex);
        }
    }

    public static bool GetReadOnly()
    {
        if (!File.Exists(FileLocation))
            return false;

        return File.GetAttributes(FileLocation).HasFlag(FileAttributes.ReadOnly);
    }

    public void CreateTemplate()
    {
        App.Logger.Info($"Creating template at {FileLocation}...");

        try
        {
            string? directory = Path.GetDirectoryName(FileLocation);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                App.Logger.Info($"Created directory: {directory}");
            }

            var uri = new Uri("avares://Froststrap/Resources/GlobalBasicSettings_Template.xml");
            using var resourceStream = Avalonia.Platform.AssetLoader.Open(uri);
            using var fileStream = File.Create(FileLocation);
            resourceStream.CopyTo(fileStream);

            previousReadOnlyState = GetReadOnly();
            App.Logger.Info("Template created successfully!");
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to create template at {FileLocation}", ex);
        }
    }

    public void Load()
    {
        App.Logger.Info($"Loading from {FileLocation}...");

        try
        {
            string? directory = Path.GetDirectoryName(FileLocation);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                App.Logger.Info($"Created directory: {directory}");
            }

            LoadDocument();
            App.Logger.Info("Loaded successfully!");
        }
        catch (Exception ex)
        {
            App.Logger.Error("Failed to load, recreating template...", ex);

            if (File.Exists(FileLocation))
            {
                try
                {
                    SetReadOnly(false, true);
                    File.Copy(FileLocation, FileLocation + ".broken", true);
                    App.Logger.Warn($"Kept a copy of the broken file at {FileLocation}.broken");
                }
                catch { /* Ignore backup errors */ }

                try { File.Delete(FileLocation); }
                catch { /* Ignore delete errors */ }
            }

            CreateTemplate();

            try
            {
                LoadDocument();
                App.Logger.Info("Recreated and loaded successfully!");
            }
            catch (Exception retryEx)
            {
                App.Logger.Error("Failed even after recreating!");
                App.Logger.Error(retryEx);
                Loaded = false;
            }
        }
    }

    private void LoadDocument()
    {
        XDocument document = XDocument.Load(FileLocation);

        if (!HasExpectedStructure(document))
            throw new InvalidDataException("GlobalBasicSettings has no UserGameSettings/Properties block.");

        Document = document;
        Loaded = true;
        previousReadOnlyState = GetReadOnly();
        _savedHash = ComputeHash();

        RemoveLegacyJunk();
    }

    private bool HasExpectedStructure(XDocument document)
    {
        return document.Root?.Name.LocalName == "roblox" &&
               document.XPathSelectElement(RootPaths["UserSettings"]) is not null;
    }

    private void RemoveLegacyJunk()
    {
        if (Document?.Root is null)
            return;

        var strayRoot = Document.Root.Elements()
            .Where(e => e.Name.LocalName is not ("Item" or "External" or "SharedStrings"))
            .ToList();

        var properties = Document.XPathSelectElement(RootPaths["UserSettings"]);
        var badNames = properties is null
            ? new List<XElement>()
            : properties.Elements()
                .Where(e => e.Attribute("name") is { } a && !ValidSettingName.IsMatch(a.Value))
                .ToList();

        if (strayRoot.Count == 0 && badNames.Count == 0)
            return;

        foreach (var element in strayRoot.Concat(badNames))
            element.Remove();

        App.Logger.Warn($"Removed {strayRoot.Count + badNames.Count} malformed element(s) left over from an older editor build.");
    }

    public virtual bool Save()
    {
        App.Logger.Info($"Saving to {FileLocation}...");

        if (!HasUnsavedChanges)
        {
            App.Logger.Info("No changes, skipping save.");
            return false;
        }

        if (!Loaded || Document == null)
        {
            App.Logger.Error("Save failed – document not loaded.");
            return false;
        }

        if (!HasExpectedStructure(Document))
        {
            App.Logger.Error("Save aborted – document is missing its UserGameSettings/Properties block.");
            return false;
        }

        try
        {
            string? dir = Path.GetDirectoryName(FileLocation);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to create directory: {ex.Message}");
            return false;
        }

        bool wasReadOnly = GetReadOnly();
        if (wasReadOnly)
        {
            App.Logger.Debug("File is read‑only; attempting to remove read‑only flag.");
            SetReadOnly(false, true);
        }

        try
        {
            string tempFile = FileLocation + ".tmp";
            Document.Save(tempFile);

            File.Copy(tempFile, FileLocation, true);
            File.Delete(tempFile);

            _savedHash = ComputeHash();
            App.Logger.Info("Save complete!");
            return true;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to save: {ex.Message}");
            App.Logger.Error(ex);
            return false;
        }
        finally
        {
            if (wasReadOnly)
            {
                App.Logger.Debug("Restoring read‑only flag.");
                SetReadOnly(true, true);
            }
        }
    }

    private static string ResolvePath(string rawPath, Dictionary<string, string> rootPaths)
    {
        return Regex.Replace(rawPath, @"\{(.+?)\}", match =>
        {
            string key = match.Groups[1].Value;
            return rootPaths.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    public string GetVectorValue(string vectorName, string axis)
    {
        if (!PresetPaths.TryGetValue(vectorName, out string? rawPath))
            return "0";

        XElement? vectorElement = Document?.XPathSelectElement(ResolvePath(rawPath, RootPaths));
        return vectorElement?.Element(axis)?.Value ?? "0";
    }

    public void SetVectorValue(string vectorName, string axis, string value)
    {
        if (!PresetPaths.TryGetValue(vectorName, out string? rawPath))
        {
            App.Logger.Warn($"SetVectorValue: Unknown vector '{vectorName}'");
            return;
        }

        if (axis is not ("X" or "Y"))
        {
            App.Logger.Warn($"SetVectorValue: Invalid axis '{axis}' for vector '{vectorName}'");
            return;
        }

        if (!TryNormalizeFloat(value, out string normalized))
        {
            App.Logger.Warn($"SetVectorValue: Rejected value '{value}' for '{vectorName}.{axis}'");
            return;
        }

        string path = ResolvePath(rawPath, RootPaths);
        XElement? vectorElement = Document?.XPathSelectElement(path) ?? CreateMissingElement(path);

        if (vectorElement is null)
            return;

        XElement? axisElement = vectorElement.Element(axis);
        if (axisElement == null)
        {
            axisElement = new XElement(axis);
            vectorElement.Add(axisElement);
            App.Logger.Info($"SetVectorValue: Created missing axis '{axis}' for vector '{vectorName}'");
        }
        axisElement.Value = normalized;
    }

    public static bool ExportSettings(string exportPath)
    {
        try
        {
            if (!File.Exists(FileLocation)) return false;
            string? dir = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(FileLocation, exportPath, true);
            return true;
        }
        catch { return false; }
    }

    public bool ImportSettings(string importPath)
    {
        try
        {
            if (!File.Exists(importPath)) return false;
            SetReadOnly(false, true);
            File.Copy(importPath, FileLocation, true);
            Load();
            return true;
        }
        catch { return false; }
    }
}
