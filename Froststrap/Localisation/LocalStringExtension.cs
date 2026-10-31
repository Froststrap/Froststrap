// SPDX-FileCopyrightText: 2026 Froststrap
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Markup.Xaml;

namespace Froststrap.Localisation;

public class LocalStringExtension : MarkupExtension
{
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return Localizer.Instance.Get(Key ?? "") ?? Key ?? "";
    }
}
