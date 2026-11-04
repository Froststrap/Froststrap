// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Froststrap.UI.Elements.Controls
{
    // Standard settings page section heading: space above, bold title, optional markdown description.
    // Set Margin="0,0,0,3" on the first header of a page, since the page already has its own padding.
    internal class SectionHeader : StackPanel
    {
        public static readonly StyledProperty<string?> TitleProperty =
            AvaloniaProperty.Register<SectionHeader, string?>(nameof(Title));

        public static readonly StyledProperty<string?> DescriptionProperty =
            AvaloniaProperty.Register<SectionHeader, string?>(nameof(Description));

        private readonly TextBlock _title = new()
        {
            FontSize = 20,
            FontWeight = FontWeight.Bold
        };

        private readonly MarkdownTextBlock _description = new()
        {
            Margin = new Thickness(0, 1, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };

        public string? Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string? Description
        {
            get => GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        static SectionHeader()
        {
            TitleProperty.Changed.AddClassHandler<SectionHeader>((x, e) => x._title.Text = e.NewValue as string);
            DescriptionProperty.Changed.AddClassHandler<SectionHeader>((x, e) =>
            {
                var text = e.NewValue as string;
                x._description.MarkdownText = text ?? string.Empty;
                x._description.IsVisible = !string.IsNullOrEmpty(text);
            });
        }

        public SectionHeader()
        {
            Margin = new Thickness(0, 12, 0, 3);
            Children.Add(_title);
            Children.Add(_description);
        }
    }
}
