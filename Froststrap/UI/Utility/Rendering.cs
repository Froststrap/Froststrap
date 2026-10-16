// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;

namespace Froststrap.UI.Utility
{
    internal static class Rendering
    {
        private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(2);

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

        public static async Task WaitForFrameAsync()
        {
            Compositor? compositor = Compositor.TryGetDefaultCompositor();

            if (compositor is null)
                return;

            try
            {
                Task rendered = compositor.RequestCompositionBatchCommitAsync().Rendered;

                await rendered.WaitAsync(FrameTimeout);
            }
            catch (TimeoutException)
            {
                App.Logger.Warn("Timed out waiting for a rendered frame");
            }
            catch (Exception ex)
            {
                App.Logger.Warn("Failed waiting for a rendered frame");
                App.Logger.Error(ex);
            }
        }
    }
}