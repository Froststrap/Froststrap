using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System.Xml.Linq;
using System.Security.Cryptography;
using FontFamily = Avalonia.Media.FontFamily;

namespace Froststrap.UI.Elements.Bootstrapper
{
    internal partial class CustomDialog
    {
        private struct GetImageSourceDataResult
        {
            public bool IsIcon = false;
            public string? Path = null;

            public GetImageSourceDataResult() { }
        }

        /// <summary>
        /// General parser for attributes. Handles both structs (Enums, int) and classes.
        /// </summary>
        private static T ParseXmlAttribute<T>(XElement element, string attributeName, T defaultValue)
        {
            var attribute = element.Attribute(attributeName);

            if (attribute == null || string.IsNullOrWhiteSpace(attribute.Value))
            {
                return defaultValue;
            }

            try
            {
                if (typeof(T) == typeof(bool))
                {
                    return (T)(object)bool.Parse(attribute.Value);
                }

                var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(T));
                if (converter != null && converter.CanConvertFrom(typeof(string)))
                {
                    return (T)converter.ConvertFromInvariantString(attribute.Value)!;
                }

                return (T)Convert.ChangeType(attribute.Value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Version for Nullable structs (like int?, double?)
        /// </summary>
        private static T? ParseXmlAttributeNullable<T>(XElement element, string attributeName) where T : struct
        {
            var attribute = element.Attribute(attributeName);
            if (attribute == null)
                return null;

            return ConvertValue<T>(attribute.Value);
        }

        private static void ValidateXmlElement(string elementName, string attributeName, double value, double? min = null, double? max = null)
        {
            if (min != null && value < min)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMustBeLargerThanMin", elementName, attributeName, min);
            if (max != null && value > max)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMustBeSmallerThanMax", elementName, attributeName, max);
        }

        private static int ParseXmlAttributeClamped(XElement element, string attributeName, int defaultValue = 0, int? min = null, int? max = null)
        {
            int value = ParseXmlAttribute<int>(element, attributeName, defaultValue);
            ValidateXmlElement(element.Name.ToString(), attributeName, (double)value, min != null ? (double)min : null, max != null ? (double)max : null);
            return value;
        }

        private static FontWeight GetFontWeightFromXElement(XElement element)
        {
            string value = element.Attribute("FontWeight")?.Value ?? "Normal";

            return value.ToUpperInvariant() switch
            {
                "THIN" => FontWeight.Thin,
                "EXTRALIGHT" or "ULTRALIGHT" => FontWeight.ExtraLight,
                "LIGHT" => FontWeight.Light,
                "NORMAL" or "REGULAR" => FontWeight.Normal,
                "MEDIUM" => FontWeight.Medium,
                "DEMIBOLD" or "SEMIBOLD" => FontWeight.SemiBold,
                "BOLD" => FontWeight.Bold,
                "EXTRABOLD" or "ULTRABOLD" => FontWeight.ExtraBold,
                "BLACK" or "HEAVY" => FontWeight.Black,
                "EXTRABLACK" or "ULTRABLACK" => FontWeight.ExtraBlack, //i just noticed ExtraBlack was mispelled :joy:
                _ => throw new CustomThemeException("CustomTheme.Errors.UnknownEnumValue", element.Name, "FontWeight", value)
            };
        }

        private static FontStyle GetFontStyleFromXElement(XElement element)
        {
            string value = element.Attribute("FontStyle")?.Value ?? "Normal";

            return value.ToUpperInvariant() switch
            {
                "NORMAL" => FontStyle.Normal,
                "ITALIC" => FontStyle.Italic,
                "OBLIQUE" => FontStyle.Oblique,
                _ => throw new CustomThemeException("CustomTheme.Errors.UnknownEnumValue", element.Name, "FontStyle", value)
            };
        }

        private static TextDecorationCollection? GetTextDecorationsFromXElement(XElement element)
        {
            string? value = element.Attribute("TextDecorations")?.Value;
            if (string.IsNullOrEmpty(value))
                return null;

            return value.ToUpperInvariant() switch
            {
                "UNDERLINE" => TextDecorations.Underline,
                "STRIKETHROUGH" => TextDecorations.Strikethrough,
                "OVERLINE" => TextDecorations.Overline,
                "BASELINE" => TextDecorations.Baseline,
                _ => throw new CustomThemeException("CustomTheme.Errors.UnknownEnumValue", element.Name, "TextDecorations", value)
            };
        }

        private static string? GetTranslatedText(string? text)
        {
            if (text == null || !text.StartsWith('{') || !text.EndsWith('}'))
                return text;

            string resourceName = text[1..^1];
            if (resourceName == "Version")
                return App.Version;

            return Strings.ResourceManager.GetString(resourceName, CultureInfo.CurrentCulture) ?? resourceName;
        }

        private static string? GetFullPath(CustomDialog dialog, string? sourcePath)
        {
            if (sourcePath == null) return null;

            if (sourcePath.StartsWith("file://", StringComparison.Ordinal))
            {
                string pathWithoutFile = sourcePath["file://".Length..];
                if (pathWithoutFile.StartsWith('/'))
                    pathWithoutFile = pathWithoutFile[1..];
                pathWithoutFile = Environment.ExpandEnvironmentVariables(pathWithoutFile);

                if (File.Exists(pathWithoutFile)) return pathWithoutFile;
                return Path.GetFullPath(pathWithoutFile);
            }

            if (sourcePath.StartsWith("theme://", StringComparison.Ordinal))
            {
                string relativePath = sourcePath["theme://".Length..];
                string fullPath = Path.Combine(dialog.ThemeDir, relativePath);
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(fullPath));
            }

            string normalizedPath = sourcePath.Replace('\\', Path.DirectorySeparatorChar);
            normalizedPath = Environment.ExpandEnvironmentVariables(normalizedPath);
            if (Path.IsPathRooted(normalizedPath)) return normalizedPath;

            return Path.GetFullPath(Path.Combine(dialog.ThemeDir, normalizedPath));
        }

        private static GetImageSourceDataResult GetImageSourceData(CustomDialog dialog, string name, XElement xmlElement)
        {
            string? path = xmlElement.Attribute(name)?.Value;
            if (string.IsNullOrEmpty(path))
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMissing", xmlElement.Name, name);

            if (path == "{Icon}")
                return new GetImageSourceDataResult { IsIcon = true };

            path = GetFullPath(dialog, path)!;

            if (!File.Exists(path))
                throw new CustomThemeException("CustomTheme.Errors.FileNotFound", path);

            return new GetImageSourceDataResult { Path = path };
        }

        private static object? GetContentFromXElement(CustomDialog dialog, XElement xmlElement)
        {
            var contentAttr = xmlElement.Attribute("Content");
            var contentElement = xmlElement.Element($"{xmlElement.Name}.Content");

            if (contentAttr != null && contentElement != null)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMultipleDefinitions", xmlElement.Name, "Content");

            if (contentAttr != null)
                return GetTranslatedText(contentAttr.Value);

            if (contentElement == null)
                return null;

            var children = contentElement.Elements().ToList();
            if (children.Count > 1)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMultipleChildren", xmlElement.Name, "Content");

            var first = children.FirstOrDefault();
            _ = first ?? throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMissingChild", xmlElement.Name, "Content");

            return HandleXml<Control>(dialog, first);
        }

        private static void ApplyEffects_Control(CustomDialog dialog, Control uiElement, XElement xmlElement)
        {
            var effectElement = xmlElement.Element($"{xmlElement.Name}.Effect");
            if (effectElement == null) return;

            var child = effectElement.Elements().FirstOrDefault();
            if (child == null) return;

            if (child.Name.LocalName == "DropShadowEffect")
            {
                var shadow = HandleXmlElement_DropShadowEffect(dialog, child);
                if (shadow is BoxShadows bxs && uiElement is Avalonia.Controls.Border border)
                {
                    border.BoxShadow = bxs;
                }
            }
            else if (child.Name.LocalName == "BlurEffect")
            {
                var effect = HandleXmlElement_BlurEffect(dialog, child);
                if (effect is IEffect blurEffect)
                {
                    uiElement.Effect = blurEffect;
                }
            }
        }

        private static void ApplyTransformations_Control(CustomDialog dialog, Control uiElement, XElement xmlElement)
        {
            var transformElement = xmlElement.Element($"{xmlElement.Name}.RenderTransform");
            if (transformElement == null) return;

            var tg = new TransformGroup();
            foreach (var child in transformElement.Elements())
            {
                var element = HandleXml<Transform>(dialog, child);
                if (element != null)
                    tg.Children.Add(element);
            }
            uiElement.RenderTransform = tg;
        }


        private static readonly string[] _fontFileExtensions = [".ttf", ".otf", ".ttc"];

        private readonly record struct LoadedFont(string Name, string Reference);

        private static readonly Dictionary<string, LoadedFont?> _customFontCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Lock _customFontLock = new();

        private static LoadedFont? LoadSingleFontFile(string path)
        {
            lock (_customFontLock)
            {
                if (_customFontCache.TryGetValue(path, out var cached))
                    return cached;

                LoadedFont? result = null;

                try
                {
                    string key = "fonts:froststrap-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)));

                    using var collection = new EmbeddedFontCollection(new Uri(key, UriKind.Absolute), new Uri(path, UriKind.Absolute));
                    FontManager.Current.AddFontCollection(collection);

                    string? familyName = collection.Select(f => f.Name).FirstOrDefault();
                    if (familyName != null)
                        result = new LoadedFont(familyName, $"{key}#{familyName}");
                }
                catch
                {
                    result = null;
                }

                _customFontCache[path] = result;
                return result;
            }
        }

        private static string? FindFontFamilyInDirectory(string directory, string familyName)
        {
            string upperName = familyName.ToUpperInvariant();

            foreach (var ext in _fontFileExtensions)
            {
                string candidate = Path.Combine(directory, upperName + ext);
                if (File.Exists(candidate))
                    return LoadSingleFontFile(candidate)?.Reference;
            }

            foreach (var ext in _fontFileExtensions)
            {
                string[] files;
                try
                {
                    files = Directory.GetFiles(directory, $"*{ext}", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    continue;
                }

                foreach (var file in files)
                {
                    var loaded = LoadSingleFontFile(file);
                    if (loaded is { } font && font.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase))
                        return font.Reference;
                }
            }

            return null;
        }

        private static bool LooksLikePathSegment(string segment)
        {
            int hashIdx = segment.LastIndexOf('#');
            string locationPart = hashIdx >= 0 ? segment[..hashIdx] : segment;

            if (locationPart.Length == 0)
                return false;

            if (locationPart.StartsWith("theme://", StringComparison.OrdinalIgnoreCase) ||
                locationPart.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                return true;

            if (locationPart.IndexOfAny(['/', '\\']) >= 0)
                return true;

            string ext = Path.GetExtension(locationPart);
            return ext.Equals(".TTF", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".OTF", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".TTC", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ResolveFontFileSegment(CustomDialog dialog, string segment)
        {
            int hashIdx = segment.LastIndexOf('#');
            string locationRaw = hashIdx >= 0 ? segment[..hashIdx] : segment;
            string? nameHint = hashIdx >= 0 ? segment[(hashIdx + 1)..].Trim() : null;
            if (string.IsNullOrEmpty(nameHint))
                nameHint = null;

            string? resolvedLocation;
            try
            {
                resolvedLocation = GetFullPath(dialog, locationRaw);
            }
            catch
            {
                resolvedLocation = null;
            }

            if (string.IsNullOrEmpty(resolvedLocation))
                return null;

            if (File.Exists(resolvedLocation))
                return LoadSingleFontFile(resolvedLocation)?.Reference;

            if (nameHint != null && Directory.Exists(resolvedLocation))
                return FindFontFamilyInDirectory(resolvedLocation, nameHint);

            return null;
        }

        private static string? ResolveFontFamilySegment(CustomDialog dialog, string rawSegment)
        {
            string segment = rawSegment.Trim();
            if (segment.Length == 0)
                return null;

            if (LooksLikePathSegment(segment))
                return ResolveFontFileSegment(dialog, segment);

            return segment.StartsWith('#') ? segment[1..].Trim() : segment;
        }

        private static void ApplyFontFamily(CustomDialog dialog, object target, XElement xmlElement)
        {
            string? fontFamilyRaw = xmlElement.Attribute("FontFamily")?.Value;
            if (string.IsNullOrWhiteSpace(fontFamilyRaw))
                return;

            FontFamily? fontFamily;

            try
            {
                var resolved = fontFamilyRaw
                    .Split(',')
                    .Select(segment => ResolveFontFamilySegment(dialog, segment))
                    .Where(segment => !string.IsNullOrEmpty(segment))
                    .ToList();

                fontFamily = resolved.Count > 0 ? new FontFamily(string.Join(", ", resolved)) : null;
            }
            catch
            {
                fontFamily = null;
            }

            if (fontFamily == null)
                return;

            switch (target)
            {
                case TemplatedControl templatedControl:
                    templatedControl.FontFamily = fontFamily;
                    break;
                case TextBlock textBlock:
                    textBlock.FontFamily = fontFamily;
                    break;
            }
        }
    }
}