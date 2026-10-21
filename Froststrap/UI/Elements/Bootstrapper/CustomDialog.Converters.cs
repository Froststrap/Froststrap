// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Windows.Input;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Avalonia.Media;
using Froststrap.UI.ViewModels.Bootstrapper;

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

        private static CompiledBinding CreateBinding(string path) => CreateBinding(path, BindingMode.OneWay);

        private static CompiledBinding CreateBinding(string path, BindingMode mode)
        {
            CompiledBindingPath bindingPath = path switch
            {
                "Icon" => BuildBindingPath(new ClrPropertyInfo(
                    "Icon",
                    static target => ((BootstrapperDialogViewModel)target).Icon,
                    null,
                    typeof(IImage))),
                "CancelEnabled" => BuildBindingPath(new ClrPropertyInfo(
                    "CancelEnabled",
                    static target => ((BootstrapperDialogViewModel)target).CancelEnabled,
                    static (target, value) => ((BootstrapperDialogViewModel)target).CancelEnabled = (bool)value!,
                    typeof(bool))),
                "CancelInstallCommand" => BuildBindingPath(new ClrPropertyInfo(
                    "CancelInstallCommand",
                    static target => ((BootstrapperDialogViewModel)target).CancelInstallCommand,
                    null,
                    typeof(ICommand))),
                "ProgressIndeterminate" => BuildBindingPath(new ClrPropertyInfo(
                    "ProgressIndeterminate",
                    static target => ((BootstrapperDialogViewModel)target).ProgressIndeterminate,
                    null,
                    typeof(bool))),
                "ProgressMaximum" => BuildBindingPath(new ClrPropertyInfo(
                    "ProgressMaximum",
                    static target => ((BootstrapperDialogViewModel)target).ProgressMaximum,
                    null,
                    typeof(int))),
                "ProgressValue" => BuildBindingPath(new ClrPropertyInfo(
                    "ProgressValue",
                    static target => ((BootstrapperDialogViewModel)target).ProgressValue,
                    null,
                    typeof(int))),
                "Message" => BuildBindingPath(new ClrPropertyInfo(
                    "Message",
                    static target => ((BootstrapperDialogViewModel)target).Message,
                    null,
                    typeof(string))),
                _ => throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown custom dialog binding path.")
            };

            return new CompiledBinding(bindingPath) { Mode = mode };
        }

        private static CompiledBindingPath BuildBindingPath(ClrPropertyInfo property)
            => new CompiledBindingPathBuilder()
                .Property(property, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)
                .Build();

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