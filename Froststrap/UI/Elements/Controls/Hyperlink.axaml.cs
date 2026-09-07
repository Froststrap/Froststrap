// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;

namespace Froststrap.UI.Elements.Controls
{
    internal class Hyperlink : Button
    {
        public static readonly StyledProperty<string?> UrlProperty =
            AvaloniaProperty.Register<Hyperlink, string?>(nameof(Url));

        public string? Url
        {
            get => GetValue(UrlProperty);
            set => SetValue(UrlProperty, value);
        }

        public string? Text
        {
            get => Content as string;
            set => Content = value;
        }

        public Hyperlink() { }

        public Hyperlink(string text, string url)
        {
            Content = text;
            Url = url;
        }

        protected override void OnClick()
        {
            base.OnClick();
            if (!string.IsNullOrEmpty(Url))
            {
                Utilities.ShellExecute(Url);
            }
        }
    }
}