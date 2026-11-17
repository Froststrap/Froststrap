// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Froststrap.UI.Converters;

internal sealed class UnixTimeToRelativeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long unixSeconds)
            return "";

        try
        {
            var date = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            var now = DateTimeOffset.UtcNow;
            var diff = now - date;

            if (diff.TotalSeconds < 60)
                return "Just now";
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours} hours ago";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays} days ago";
            if (diff.TotalDays < 30)
                return $"{(int)(diff.TotalDays / 7)} weeks ago";
            if (diff.TotalDays < 365)
                return $"{(int)(diff.TotalDays / 30)} months ago";

            return $"{(int)(diff.TotalDays / 365)} years ago";
        }
        catch
        {
            return "";
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
internal sealed class NewsSelectionBorderConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true &&
            Application.Current?.TryGetResource("SystemAccentColor", null, out var resource) == true &&
            resource is Color color)
            return new SolidColorBrush(color);

        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
