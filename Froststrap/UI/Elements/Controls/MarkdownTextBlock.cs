using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Froststrap.UI.Elements.Controls
{
    class MarkdownTextBlock : TextBlock
    {
        private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
            .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Marked)
            .UseSoftlineBreakAsHardlineBreak()
            .Build();

        public static readonly StyledProperty<string> MarkdownTextProperty =
            AvaloniaProperty.Register<MarkdownTextBlock, string>(
                nameof(MarkdownText),
                defaultValue: string.Empty,
                defaultBindingMode: BindingMode.OneWay,
                coerce: OnCoerceMarkdownText);

        public string MarkdownText
        {
            get => GetValue(MarkdownTextProperty);
            set => SetValue(MarkdownTextProperty, value);
        }

        static MarkdownTextBlock()
        {
            MarkdownTextProperty.Changed.AddClassHandler<MarkdownTextBlock>((x, e) => x.OnMarkdownTextChanged(e));
        }

        private static string OnCoerceMarkdownText(AvaloniaObject sender, string value)
        {
            if (sender is MarkdownTextBlock markdownTextBlock && value != null)
            {
                markdownTextBlock.UpdateMarkdown(value);
            }
            return value ?? string.Empty;
        }

        private void OnMarkdownTextChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is string markdown)
            {
                UpdateMarkdown(markdown);
            }
        }

        private void UpdateMarkdown(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                Inlines?.Clear();
                return;
            }

            var document = Markdig.Markdown.Parse(markdown, _markdownPipeline);
            var result = ConvertMarkdownToInlines(document);

            if (Inlines != null)
            {
                Inlines.Clear();
                Inlines.AddRange(result);
            }
        }

        private InlineCollection ConvertMarkdownToInlines(MarkdownDocument document)
        {
            var inlines = new InlineCollection();
            var lastBlock = document.LastChild;

            foreach (var block in document)
            {
                if (block is not ParagraphBlock paragraphBlock || paragraphBlock.Inline == null)
                    continue;

                foreach (var inline in paragraphBlock.Inline)
                {
                    var avaloniaInline = GetAvaloniaInlineFromMarkdownInline(inline);
                    if (avaloniaInline != null)
                        inlines.Add(avaloniaInline);
                }

                if (block != lastBlock)
                {
                    inlines.Add(new LineBreak());
                    inlines.Add(new LineBreak());
                }
            }

            return inlines;
        }

        private Avalonia.Controls.Documents.Inline? GetAvaloniaInlineFromMarkdownInline(Markdig.Syntax.Inlines.Inline? inline)
        {
            if (inline == null) return null;

            if (inline is LiteralInline literalInline)
            {
                return new Run(literalInline.ToString());
            }

            if (inline is EmphasisInline emphasisInline)
            {
                var childInline = GetAvaloniaInlineFromMarkdownInline(emphasisInline.FirstChild);
                if (childInline == null) return null;

                switch (emphasisInline.DelimiterChar)
                {
                    case '*':
                    case '_':
                        if (emphasisInline.DelimiterCount == 1)
                        {
                            var italic = new Italic();
                            italic.Inlines.Add(childInline);
                            return italic;
                        }
                        var bold = new Bold();
                        bold.Inlines.Add(childInline);
                        return bold;
                    case '=':
                        var span = new Span();
                        span.Inlines.Add(childInline);
                        span.Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                        return span;
                    default:
                        return childInline;
                }
            }

            if (inline is LinkInline linkInline)
            {
                string url = linkInline.Url ?? string.Empty;
                var textInline = linkInline.FirstChild;
                string linkText = textInline?.ToString() ?? url;

                var textBlock = new TextBlock
                {
                    Text = linkText
                };

                var hyperlinkControl = new Hyperlink(linkText, url)
                {
                    Content = textBlock
                };

                textBlock[!TextBlock.ForegroundProperty] = hyperlinkControl[!Hyperlink.ForegroundProperty];

                hyperlinkControl.PointerEntered += (s, e) => textBlock.TextDecorations = Avalonia.Media.TextDecorations.Underline;
                hyperlinkControl.PointerExited += (s, e) => textBlock.TextDecorations = null;

                return new InlineUIContainer(hyperlinkControl)
                {
                    BaselineAlignment = BaselineAlignment.TextBottom
                };
            }

            if (inline is LineBreakInline) return new LineBreak();

            return null;
        }
    }
}