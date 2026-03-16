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

        public static readonly StyledProperty<IBrush> LinkForegroundProperty =
            AvaloniaProperty.Register<MarkdownTextBlock, IBrush>(
                nameof(LinkForeground),
                defaultValue: Brushes.LightBlue);

        public string MarkdownText
        {
            get => GetValue(MarkdownTextProperty);
            set => SetValue(MarkdownTextProperty, value);
        }

        public IBrush LinkForeground
        {
            get => GetValue(LinkForegroundProperty);
            set => SetValue(LinkForegroundProperty, value);
        }

        static MarkdownTextBlock()
        {
            MarkdownTextProperty.Changed.AddClassHandler<MarkdownTextBlock>((x, e) => x.OnMarkdownTextChanged(e));
        }

        private static string OnCoerceMarkdownText(AvaloniaObject sender, string value)
        {
            if (sender is MarkdownTextBlock markdownTextBlock)
            {
                markdownTextBlock.UpdateMarkdown(value);
            }
            return value;
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
            Inlines = ConvertMarkdownToInlines(document);
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
            if (inline is LiteralInline literalInline)
            {
                return new Run(literalInline.ToString());
            }
            else if (inline is EmphasisInline emphasisInline)
            {
                switch (emphasisInline.DelimiterChar)
                {
                    case '*':
                    case '_':
                        {
                            var childInline = GetAvaloniaInlineFromMarkdownInline(emphasisInline.FirstChild);
                            if (childInline == null) return null;

                            if (emphasisInline.DelimiterCount == 1)
                            {
                                var italic = new Italic();
                                italic.Inlines.Add(childInline);
                                return italic;
                            }
                            else
                            {
                                var bold = new Bold();
                                bold.Inlines.Add(childInline);
                                return bold;
                            }
                        }

                    case '=':
                        {
                            var childInline = GetAvaloniaInlineFromMarkdownInline(emphasisInline.FirstChild);
                            if (childInline == null) return null;

                            var span = new Span();
                            span.Inlines.Add(childInline);
                            span.Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                            return span;
                        }
                }
            }
            else if (inline is LinkInline linkInline)
            {
                string? url = linkInline.Url;
                var textInline = linkInline.FirstChild;

                if (string.IsNullOrEmpty(url))
                    return GetAvaloniaInlineFromMarkdownInline(textInline);

                string linkText = textInline?.ToString() ?? "";

                var hyperlinkControl = new Hyperlink(linkText, url)
                {
                    LinkForeground = LinkForeground
                };

                if (hyperlinkControl.Content == null)
                {
                    hyperlinkControl.Content = new TextBlock
                    {
                        Text = linkText,
                        TextDecorations = TextDecorationCollection.Parse("Underline")
                    };
                }

                return new InlineUIContainer(hyperlinkControl)
                {
                    BaselineAlignment = BaselineAlignment.Center
                };
            }
            else if (inline is LineBreakInline)
            {
                return new LineBreak();
            }

            return null;
        }
    }
}