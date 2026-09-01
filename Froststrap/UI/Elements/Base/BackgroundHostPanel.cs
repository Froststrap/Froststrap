using Avalonia;
using Avalonia.Controls;

namespace Froststrap.UI.Elements.Base
{
    internal class BackgroundHostPanel : Panel
    {
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