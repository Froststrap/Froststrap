using Avalonia.Controls;
using Avalonia.Media;

namespace Froststrap.UI.Utility
{
    public static class Rendering
    {
        public static double GetTextWidth(TextBlock textBlock)
        {
            if (textBlock == null) return 0;
            return GetTextWidth(textBlock.Text ?? "", textBlock.FontFamily, textBlock.FontSize);
        }

        public static double GetTextWidth(string text, Avalonia.Media.FontFamily fontFamily, double fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            try
            {
                var formattedText = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(fontFamily),
                    fontSize,
                    Brushes.Black
                );

                return formattedText.Width;
            }
            catch (Exception)
            {
                return text.Length * fontSize * 0.6;
            }
        }
    }
}