// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;

namespace Froststrap.UI.Elements.Bootstrapper
{
    internal partial class CustomDialog
    {
        private static T? ConvertValue<T>(string input) where T : struct
        {
            try
            {
                if (typeof(T).IsEnum)
                    return Enum.Parse<T>(input, ignoreCase: true);

                return (T?)Convert.ChangeType(input, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (NotSupportedException)
            {
                return default;
            }
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Custom theme bindings target a runtime DataContext, so reflection bindings are required; the dialog view models are rooted.")]
        private static Binding ReflectionBinding(string path) => new(path);

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Custom theme bindings target a runtime DataContext, so reflection bindings are required; the dialog view models are rooted.")]
        private static Binding ReflectionBinding(string path, BindingMode mode) => new(path) { Mode = mode };

        private static Thickness? GetThicknessFromXElement(XElement xmlElement, string attributeName)
        {
            string? value = xmlElement.Attribute(attributeName)?.Value;
            if (string.IsNullOrEmpty(value))
                return null;

            try
            {
                return Thickness.Parse(value);
            }
            catch (Exception ex)
            {
                throw new CustomThemeException(ex, "CustomTheme.Errors.ElementAttributeConversionError",
                    xmlElement.Name, attributeName, $"Could not parse '{value}' as Thickness");
            }
        }

        private static Color? GetColorFromXElement(XElement xmlElement, string attributeName)
        {
            string? value = xmlElement.Attribute(attributeName)?.Value;
            if (string.IsNullOrEmpty(value))
                return null;

            try
            {
                return Color.Parse(value);
            }
            catch (Exception ex)
            {
                throw new CustomThemeException(ex, "CustomTheme.Errors.ElementAttributeConversionError",
                    xmlElement.Name, attributeName, $"Could not parse '{value}' as Color");
            }
        }

        private static Point? GetPointFromXElement(XElement xmlElement, string attributeName)
        {
            string? value = xmlElement.Attribute(attributeName)?.Value;
            if (string.IsNullOrEmpty(value))
                return null;

            try
            {
                return Point.Parse(value);
            }
            catch (Exception ex)
            {
                throw new CustomThemeException(ex, "CustomTheme.Errors.ElementAttributeConversionError",
                    xmlElement.Name, attributeName, $"Could not parse '{value}' as Point");
            }
        }

        private static CornerRadius? GetCornerRadiusFromXElement(XElement xmlElement, string attributeName)
        {
            string? value = xmlElement.Attribute(attributeName)?.Value;
            if (string.IsNullOrEmpty(value))
                return null;

            try
            {
                return CornerRadius.Parse(value);
            }
            catch (Exception ex)
            {
                throw new CustomThemeException(ex, "CustomTheme.Errors.ElementAttributeConversionError",
                    xmlElement.Name, attributeName, $"Could not parse '{value}' as CornerRadius");
            }
        }

        private static GridLength? GetGridLengthFromXElement(XElement xmlElement, string attributeName)
        {
            string? value = xmlElement.Attribute(attributeName)?.Value;
            if (string.IsNullOrEmpty(value))
                return null;

            try
            {
                return GridLength.Parse(value);
            }
            catch (Exception ex)
            {
                throw new CustomThemeException(ex, "CustomTheme.Errors.ElementAttributeConversionError",
                    xmlElement.Name, attributeName, $"Could not parse '{value}' as GridLength");
            }
        }

        private static object? GetBrushFromXElement(XElement element, string attributeName)
        {
            string? value = element.Attribute(attributeName)?.Value;
            if (string.IsNullOrEmpty(value))
                return null;

            if (value.StartsWith('{') && value.EndsWith('}'))
                return value[1..^1];

            try
            {
                return Brush.Parse(value);
            }
            catch (Exception ex)
            {
                throw new CustomThemeException(ex, "CustomTheme.Errors.ElementAttributeConversionError",
                    element.Name, attributeName, ex.Message);
            }
        }
    }
}