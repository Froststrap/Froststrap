// SPDX-FileCopyrightText: 2026 Froststrap
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Froststrap.Localisation;

public sealed class Localizer
{
    private static readonly Localizer s_instance = new();
    public static Localizer Instance => s_instance;

    static Localizer()
    {
        // Eager load default embedded translations so XAML static bindings resolve
        try
        {
            var assembly = typeof(Localizer).Assembly;
            var defaultName = "Froststrap.Resources.Translations.en-US.json";
            var stream = assembly.GetManifestResourceStream(defaultName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();
                s_instance._default.Clear();
                MergeFromJson(text, s_instance._default);
            }
            // Load available cultures eagerly
            foreach (var resName in assembly.GetManifestResourceNames())
            {
                if (resName.StartsWith("Froststrap.Resources.Translations.") && resName.EndsWith(".json"))
                {
                    var cult = resName.Substring("Froststrap.Resources.Translations.".Length);
                    cult = cult.Substring(0, cult.Length - ".json".Length);
                    if (cult == "en-US") continue; // already default
                    using var s = assembly.GetManifestResourceStream(resName);
                    if (s != null)
                    {
                        using var r = new StreamReader(s);
                        var dict = new Dictionary<string, string>();
                        MergeFromJson(r.ReadToEnd(), dict);
                        s_instance._cultures[cult] = dict;
                    }
                }
            }
        }
        catch { /* fail safe */ }
    }

    private readonly Dictionary<string, string> _default = new();
    private readonly Dictionary<string, Dictionary<string, string>> _cultures = new();
    private string _cultureName = "en-US";

    public string CultureName
    {
        get => _cultureName;
        set => _cultureName = value ?? "en-US";
    }

    public void LoadDefault(string filePath)
    {
        if (File.Exists(filePath))
        {
            var text = File.ReadAllText(filePath);
            _default.Clear();
            MergeFromJson(text, _default);
        }
    }

    public void LoadCulture(string cultureName, string filePath)
    {
        if (File.Exists(filePath))
        {
            var text = File.ReadAllText(filePath);
            var dict = new Dictionary<string, string>();
            MergeFromJson(text, dict);
            _cultures[cultureName] = dict;
        }
    }

    private static void MergeFromJson(string json, Dictionary<string, string> target)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    target[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
            }
        }
    }

    public string Get(string key, params object[] args)
    {
        if (string.IsNullOrEmpty(key)) return key ?? string.Empty;

        string value = TryGetInCulture(_cultureName, key)
                      ?? TryGetInCulture("en-US", key)
                      ?? TryGetInDefault(key)
                      ?? key;

        if (args.Length > 0)
        {
            // Preserve existing format behaviour (InvariantCulture)
            value = string.Format(CultureInfo.InvariantCulture, value, args);
        }
        return value;
    }

    private string? TryGetInCulture(string cultureName, string key)
    {
        if (_cultures.TryGetValue(cultureName, out var dict))
        {
            if (dict.TryGetValue(key, out var v))
                return v;
        }
        return null;
    }

    private string? TryGetInDefault(string key)
    {
        if (_default.TryGetValue(key, out var v))
            return v;
        return null;
    }

    public void SetCulture(string cultureName)
    {
        _cultureName = cultureName ?? "en-US";
    }
}
