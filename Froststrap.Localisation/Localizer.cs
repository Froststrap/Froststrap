// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Froststrap.Localisation
{
    public static class Localizer
    {
        public const string SourceLocale = "en-US";

        private const string ResourcePrefix = "Froststrap.Localisation.Translations.";
        private const string ResourceSuffix = ".json";

        private static readonly Assembly ResourceAssembly = typeof(Localizer).Assembly;

        private static readonly Dictionary<string, string> Source = LoadEmbedded(SourceLocale) ?? [];

        private static Dictionary<string, string>? _local;
        private static Dictionary<string, string>? _remote;

        public static string CultureName { get; private set; } = SourceLocale;

        public static event EventHandler? StringsChanged;

        public static IReadOnlyList<string> AvailableLocales { get; } = ResourceAssembly
            .GetManifestResourceNames()
            .Where(x => x.StartsWith(ResourcePrefix, StringComparison.Ordinal) && x.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            .Select(x => x[ResourcePrefix.Length..^ResourceSuffix.Length])
            .Order(StringComparer.Ordinal)
            .ToList();

        public static IReadOnlyCollection<string> Keys => Source.Keys;

        public static void SetCulture(string cultureName)
        {
            ArgumentException.ThrowIfNullOrEmpty(cultureName);

            Volatile.Write(ref _local, cultureName == SourceLocale ? null : LoadEmbedded(cultureName));
            Volatile.Write(ref _remote, null);
            CultureName = cultureName;

            StringsChanged?.Invoke(null, EventArgs.Empty);
        }

        public static int ApplyRemote(string cultureName, string json)
        {
            if (cultureName != CultureName || cultureName == SourceLocale)
                return 0;

            var parsed = Parse(json);
            var accepted = new Dictionary<string, string>(parsed.Count, StringComparer.Ordinal);

            foreach (var (key, value) in parsed)
            {
                if (!Source.TryGetValue(key, out var english))
                    continue;

                if (string.IsNullOrWhiteSpace(value) || value == english)
                    continue;

                if (!PlaceholdersCompatible(english, value))
                    continue;

                accepted[key] = value;
            }

            Volatile.Write(ref _remote, accepted);
            StringsChanged?.Invoke(null, EventArgs.Empty);

            return accepted.Count;
        }

        public static string Get(string key) => TryGet(key) ?? key;

        public static string? TryGet(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            key = NormaliseKey(key);

            if (Volatile.Read(ref _remote) is { } remote && remote.TryGetValue(key, out var value))
                return value;

            if (Volatile.Read(ref _local) is { } local && local.TryGetValue(key, out value))
                return value;

            return Source.GetValueOrDefault(key);
        }

        public static string GetEnglish(string key) =>
            string.IsNullOrEmpty(key) ? key : Source.GetValueOrDefault(NormaliseKey(key)) ?? key;

        private static string NormaliseKey(string key) =>
            key.Contains('.', StringComparison.Ordinal) ? key.Replace('.', '_') : key;

        private static Dictionary<string, string>? LoadEmbedded(string cultureName)
        {
            using var stream = ResourceAssembly.GetManifestResourceStream(ResourcePrefix + cultureName + ResourceSuffix);

            if (stream is null)
                return null;

            using var document = JsonDocument.Parse(stream);
            return ToDictionary(document);
        }

        private static Dictionary<string, string> Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            return ToDictionary(document);
        }

        private static Dictionary<string, string> ToDictionary(JsonDocument document)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                    result[property.Name] = property.Value.GetString() ?? "";
            }

            return result;
        }

        private static bool PlaceholdersCompatible(string english, string translated)
        {
            int count = 0;

            for (int i = 0; i < english.Length - 1; i++)
            {
                if (english[i] != '{')
                    continue;

                int start = i + 1;
                int end = start;

                while (end < english.Length && char.IsAsciiDigit(english[end]))
                    end++;

                if (end > start && int.TryParse(english.AsSpan(start, end - start), NumberStyles.None, CultureInfo.InvariantCulture, out int index))
                    count = Math.Max(count, index + 1);
            }

            if (count == 0)
                return true;

            try
            {
                _ = string.Format(CultureInfo.InvariantCulture, translated, new object[count]);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
