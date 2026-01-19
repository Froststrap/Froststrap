using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Metadata;
using Froststrap;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Froststrap.UI.Elements.Controls
{
	/// <summary>
	/// TextBlock with markdown support.
	/// </summary>
	public class MarkdownTextBlock : TextBlock
	{
		private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
				.UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Marked)
				.UseSoftlineBreakAsHardlineBreak()
				.Build();

		public static readonly StyledProperty<string> MarkdownTextProperty =
			AvaloniaProperty.Register<MarkdownTextBlock, string>(
				nameof(MarkdownText),
				string.Empty,
				coerce: OnMarkdownTextChanged);

		[Content]
		public string MarkdownText
		{
			get => GetValue(MarkdownTextProperty);
			set => SetValue(MarkdownTextProperty, value);
		}

		private readonly Dictionary<Run, string> _linkRuns = new Dictionary<Run, string>();

		private static string OnMarkdownTextChanged(AvaloniaObject sender, string value)
		{
			if (sender is MarkdownTextBlock markdownTextBlock)
			{
				markdownTextBlock.UpdateMarkdownContent(value);
			}
			return value;
		}

		private Avalonia.Controls.Documents.Inline? GetAvaloniaInlineFromMarkdownInline(Markdig.Syntax.Inlines.Inline? inline, string? linkUrl = null)
		{
			if (inline is LiteralInline literalInline)
			{
				var run = new Run(literalInline.ToString());

				if (!string.IsNullOrEmpty(linkUrl))
				{
					var span = new Span();
					span.Inlines.Add(run);
					span.Foreground = new SolidColorBrush(Colors.Blue);

					var decoration = new TextDecoration
					{
						Location = TextDecorationLocation.Underline
					};
					span.TextDecorations = new TextDecorationCollection { decoration };

					_linkRuns[run] = linkUrl;
					return span;
				}

				return run;
			}
			else if (inline is EmphasisInline emphasisInline)
			{
				switch (emphasisInline.DelimiterChar)
				{
					case '*':
					case '_':
						{
							var childInline = GetAvaloniaInlineFromMarkdownInline(emphasisInline.FirstChild, linkUrl);
							if (childInline == null)
								return null;

							var span = new Span();
							span.Inlines.Add(childInline);

							if (emphasisInline.DelimiterCount == 1)
							{
								span.FontStyle = FontStyle.Italic;
							}
							else
							{
								span.FontWeight = FontWeight.Bold;
							}
							return span;
						}

					case '=':
						{
							var childInline = GetAvaloniaInlineFromMarkdownInline(emphasisInline.FirstChild, linkUrl);
							if (childInline == null)
								return null;

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
					return GetAvaloniaInlineFromMarkdownInline(textInline, linkUrl);

				return GetAvaloniaInlineFromMarkdownInline(textInline, url);
			}
			else if (inline is LineBreakInline)
			{
				return new LineBreak();
			}

			return null;
		}

		private void AddMarkdownInline(Markdig.Syntax.Inlines.Inline? inline)
		{
			var avaloniaInline = GetAvaloniaInlineFromMarkdownInline(inline);

			if (avaloniaInline != null)
				Inlines!.Add(avaloniaInline);
		}

		private void UpdateMarkdownContent(string rawDocument)
		{
			if (string.IsNullOrEmpty(rawDocument))
			{
				Inlines?.Clear();
				return;
			}

			_linkRuns.Clear();

			var document = Markdig.Markdown.Parse(rawDocument, _markdownPipeline);
			Inlines!.Clear();

			var lastBlock = document.LastOrDefault();

			foreach (var block in document)
			{
				if (block is not ParagraphBlock paragraphBlock || paragraphBlock.Inline == null)
					continue;

				foreach (var inline in paragraphBlock.Inline)
					AddMarkdownInline(inline);

				if (block != lastBlock)
				{
					AddMarkdownInline(new LineBreakInline());
					AddMarkdownInline(new LineBreakInline());
				}
			}
		}

		public MarkdownTextBlock()
		{
			TextWrapping = TextWrapping.Wrap;
			this.PointerMoved += OnPointerMoved;
			this.PointerPressed += OnPointerPressed;
		}

		private void OnPointerMoved(object? sender, PointerEventArgs e)
		{
			var position = e.GetPosition(this);
			var hit = this.InputHitTest(position);

			if (hit is Run run && _linkRuns.ContainsKey(run))
			{
				this.Cursor = new Cursor(StandardCursorType.Hand);
			}
			else
			{
				this.Cursor = Cursor.Default;
			}
		}

		private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
		{
			var position = e.GetCurrentPoint(this);
			if (!position.Properties.IsLeftButtonPressed)
				return;

			var hit = this.InputHitTest(position.Position);

			if (hit is Run run && _linkRuns.TryGetValue(run, out var url))
			{
				Utilities.ShellExecute(url);
				e.Handled = true;
			}
		}
	}
}