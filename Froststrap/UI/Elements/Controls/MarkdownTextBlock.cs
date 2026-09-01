using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using AvaloniaInline = Avalonia.Controls.Documents.Inline;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace Froststrap.UI.Elements.Controls
{
    internal class MarkdownTextBlock : TextBlock
    {
        private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
            .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Marked)
            .UseSoftlineBreakAsHardlineBreak()
            .Build();

        public static readonly StyledProperty<string> MarkdownTextProperty =
            AvaloniaProperty.Register<MarkdownTextBlock, string>(
                nameof(MarkdownText),
                defaultValue: string.Empty,
                defaultBindingMode: BindingMode.OneWay);

        public string MarkdownText
        {
            get => GetValue(MarkdownTextProperty);
            set => SetValue(MarkdownTextProperty, value);
        }

        static MarkdownTextBlock()
        {
            MarkdownTextProperty.Changed.AddClassHandler<MarkdownTextBlock>((x, e) => x.UpdateMarkdown(e.NewValue as string));
            FontSizeProperty.Changed.AddClassHandler<MarkdownTextBlock>((x, e) => x.UpdateMarkdown(x.MarkdownText));
            FontFamilyProperty.Changed.AddClassHandler<MarkdownTextBlock>((x, e) => x.UpdateMarkdown(x.MarkdownText));
            FontWeightProperty.Changed.AddClassHandler<MarkdownTextBlock>((x, e) => x.UpdateMarkdown(x.MarkdownText));
        }

        private void UpdateMarkdown(string? markdown)
        {
            Inlines?.Clear();
            if (string.IsNullOrEmpty(markdown)) return;

            var document = Markdown.Parse(markdown, _markdownPipeline);
            var result = ConvertMarkdownToInlines(
                document,
                FontSize,
                FontFamily,
                FontWeight);

            Inlines!.AddRange(result);
        }

        private static List<AvaloniaInline> ConvertMarkdownToInlines(
            MarkdownDocument document,
            double fontSize,
            Avalonia.Media.FontFamily fontFamily,
            FontWeight fontWeight)
        {
            var inlines = new List<AvaloniaInline>();

            foreach (var block in document)
            {
                if (block is ParagraphBlock paragraphBlock && paragraphBlock.Inline != null)
                {
                    foreach (var inline in paragraphBlock.Inline)
                    {
                        var avaloniaInlines = GetAvaloniaInlinesFromMarkdownInline(
                            inline,
                            fontSize,
                            fontFamily,
                            fontWeight);
                        inlines.AddRange(avaloniaInlines);
                    }

                    if (block != document.LastChild)
                        inlines.Add(new LineBreak());
                }
            }

            return inlines;
        }

        private static IEnumerable<AvaloniaInline> GetAvaloniaInlinesFromMarkdownInline(
            MarkdigInline? inline,
            double fontSize,
            Avalonia.Media.FontFamily fontFamily,
            FontWeight fontWeight)
        {
            if (inline == null) yield break;

            if (inline is LiteralInline literalInline)
            {
                yield return new Run(literalInline.Content.ToString());
            }
            else if (inline is EmphasisInline emphasisInline)
            {
                var span = emphasisInline.DelimiterCount == 1 && (emphasisInline.DelimiterChar == '*' || emphasisInline.DelimiterChar == '_')
                    ? new Italic()
                    : (emphasisInline.DelimiterChar == '=' ? new Span() : new Bold());

                if (span is Bold b) b.FontWeight = FontWeight.SemiBold;

                if (emphasisInline.DelimiterChar == '=')
                {
                    span.Background = (IBrush)Application.Current!.FindResource("AccentFillColorSelectedTextBackgroundBrush")!;
                    span.Foreground = (IBrush)Application.Current!.FindResource("SystemAccentColor")!;
                }

                foreach (var child in emphasisInline)
                {
                    foreach (var childInline in GetAvaloniaInlinesFromMarkdownInline(
                        child,
                        fontSize,
                        fontFamily,
                        fontWeight))
                        span.Inlines.Add(childInline);
                }
                yield return span;
            }
            else if (inline is LinkInline linkInline)
            {
                var url = linkInline.Url ?? string.Empty;
                var linkText = linkInline.FirstChild?.ToString() ?? url;

                var hyperlinkControl = new Hyperlink
                {
                    Content = linkText,
                    Url = url,
                    FontSize = fontSize,
                    FontFamily = fontFamily,
                    FontWeight = fontWeight,
                };

                yield return new InlineUIContainer(hyperlinkControl)
                {
                    BaselineAlignment = BaselineAlignment.Center
                };
            }
            else if (inline is LineBreakInline)
            {
                yield return new LineBreak();
            }
            else if (inline is ContainerInline container)
            {
                foreach (var child in container)
                {
                    foreach (var childInline in GetAvaloniaInlinesFromMarkdownInline(
                        child,
                        fontSize,
                        fontFamily,
                        fontWeight))
                        yield return childInline;
                }
            }
        }
    }
}