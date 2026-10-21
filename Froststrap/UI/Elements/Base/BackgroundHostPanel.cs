// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Froststrap.UI.Elements.Base
{
    internal partial class BackgroundHostPanel : Panel
    {
        public BackgroundHostPanel()
        {
            InitializeComponent();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Size contentSize = default;

            foreach (var child in Children)
            {
                child.Measure(availableSize);

                if (child.Name == "PART_ContentHost")
                    contentSize = child.DesiredSize;
            }

            return contentSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (var child in Children)
                child.Arrange(new Rect(finalSize));

            return finalSize;
        }
    }
}