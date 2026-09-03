using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using FluentAvalonia.UI.Controls;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.Elements.Controls;
using Froststrap.UI.ViewModels.Editor;
using LucideAvalonia.Enum;
using System.Xml;
using Avalonia.Controls.Shapes;

namespace Froststrap.UI.Elements.Editor
{
    internal partial class BootstrapperEditorWindow : AvaloniaWindow, IDisposable
    {
        private static class CustomBootstrapperSchema
        {
            private class Schema
            {
                public Dictionary<string, Element> Elements { get; set; } = [];
                public Dictionary<string, Type> Types { get; set; } = [];
            }

            private class Element
            {
                public string? SuperClass { get; set; }
                public bool IsCreatable { get; set; }
                public Dictionary<string, string> Attributes { get; set; } = [];
            }

            internal class Type
            {
                public bool CanHaveElement { get; set; }
                public List<string>? Values { get; set; }
            }

            private static Schema? _schema;

            public static SortedDictionary<string, SortedDictionary<string, string>> ElementInfo { get; set; } = [];
            public static Dictionary<string, List<string>> PropertyElements { get; set; } = [];
            public static SortedDictionary<string, Type> Types { get; set; } = [];

            public static void ParseSchema()
            {
                if (_schema != null) return;

                try
                {
                    string json = Resource.GetString("CustomBootstrapperSchema.json").GetAwaiter().GetResult();
                    _schema = JsonSerializer.Deserialize<Schema>(json) ?? throw new InvalidOperationException("Schema deserialization failed.");

                    foreach (var type in _schema.Types)
                        Types.Add(type.Key, type.Value);

                    PopulateElementInfo();
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Critical error loading schema: {ex.Message}");
                }
            }

            private static (SortedDictionary<string, string>, List<string>) GetElementAttributes(string name, Element element)
            {
                if (ElementInfo.TryGetValue(name, out var existingAttributes))
                    return (existingAttributes, PropertyElements[name]);

                List<string> properties = [];
                SortedDictionary<string, string> attributes = [];

                foreach (var attribute in element.Attributes)
                {
                    attributes.Add(attribute.Key, attribute.Value);

                    if (Types.TryGetValue(attribute.Value, out var type))
                    {
                        if (type.CanHaveElement)
                            properties.Add(attribute.Key);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Schema for type {attribute.Value} is missing. Blame Matt!");
                    }
                }

                if (element.SuperClass != null)
                {
                    (SortedDictionary<string, string> superAttributes, List<string> superProperties) = GetElementAttributes(element.SuperClass, _schema!.Elements[element.SuperClass]);
                    foreach (var attribute in superAttributes)
                        attributes.TryAdd(attribute.Key, attribute.Value);

                    foreach (var property in superProperties)
                        if (!properties.Contains(property))
                            properties.Add(property);
                }

                properties.Sort();

                ElementInfo[name] = attributes;
                PropertyElements[name] = properties;

                return (attributes, properties);
            }

            private static void PopulateElementInfo()
            {
                List<string> toRemove = [];

                foreach (var element in _schema!.Elements)
                {
                    GetElementAttributes(element.Key, element.Value);

                    if (!element.Value.IsCreatable)
                        toRemove.Add(element.Key);
                }

                foreach (var name in toRemove)
                {
                    ElementInfo.Remove(name);
                }
            }
        }

        private readonly BootstrapperEditorWindowViewModel _viewModel = null!;
        private CompletionWindow? _completionWindow;
        private bool _isInitialLoad = true;
        private bool _disposed;

        private readonly List<NotificationEntry> _notifications = [];
        private sealed class NotificationEntry
        {
            public required Border Element { get; init; }
            public required TranslateTransform Transform { get; init; }
            public CancellationTokenSource? TimeoutCts { get; set; }
        }

        public BootstrapperEditorWindow()
        {
            InitializeComponent();
        }

        public BootstrapperEditorWindow(string name) : this()
        {
            CustomBootstrapperSchema.ParseSchema();

            string directory = System.IO.Path.Combine(Paths.CustomThemes, name);
            string themeContents = File.ReadAllText(System.IO.Path.Combine(directory, "Theme.xml"));

            _viewModel = new BootstrapperEditorWindowViewModel
            {
                Directory = directory,
                Name = name,
                Code = ToCRLF(themeContents),
                Title = string.Format(CultureInfo.InvariantCulture, Strings.CustomTheme_Editor_Title, name)
            };

            DataContext = _viewModel;

            this.Loaded += (s, e) =>
            {
                UIXML.Text = _viewModel.Code;
            };

            _viewModel.ThemeSavedCallback = (success, message) =>
            {
                if (success)
                {
                    Dispatcher.UIThread.Post(() => ShowNotification(
                        Strings.Menu_SettingsSaved_Title,
                        Strings.Menu_SettingsSaved_Message,
                        FAInfoBarSeverity.Success,
                        3000));
                }
                else
                {
                    Dispatcher.UIThread.Post(() => ShowNotification("Error", message, FAInfoBarSeverity.Error, 5000));
                }
            };

            UIXML.TextChanged += OnCodeChanged;
            UIXML.TextArea.TextEntered += OnTextEntered;

            LoadHighlightingTheme();
            this.Closing += OnClosing;
        }

        private void OnTextEntered(object? sender, TextInputEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;

            switch (e.Text)
            {
                case "<":
                    OpenElementAutoComplete();
                    break;
                case " ":
                    OpenAttributeAutoComplete();
                    break;
                case ".":
                    OpenPropertyElementAutoComplete();
                    break;
                case "/":
                    AddEndTag();
                    break;
                case ">":
                case "!":
                    CloseCompletionWindow();
                    break;
            }
        }

        private void LoadHighlightingTheme()
        {
            try
            {
                string themeName = App.Settings.Prop.Theme.GetFinal().ToString();
                var uri = new Uri($"avares://Froststrap/UI/AppThemes/EditorThemes/Editor-Theme-{themeName}.xshd");

                using var xmlStream = AssetLoader.Open(uri);
                using var reader = XmlReader.Create(xmlStream);
                UIXML.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            catch (Exception)
            {
                App.Logger.Error("Theme file not found, falling back to default XML.");
                UIXML.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
            }
        }

        public void ShowNotification(string title, string subtitle, FAInfoBarSeverity type, int timeout, LucideIconNames? customIcon = null)
        {
            var notificationPanel = this.FindControl<Panel>("NotificationPanel");
            if (notificationPanel == null) return;

            var accentColor = type == FAInfoBarSeverity.Success ? "#00D084" : "#FFB900";
            var iconSymbol = customIcon ?? (type == FAInfoBarSeverity.Success
                ? LucideIconNames.CircleCheck
                : LucideIconNames.TriangleAlert);

            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                Margin = new Thickness(0)
            };

            var icon = new Ellipse
            {
                Width = 12,
                Height = 12,
                Margin = new Thickness(25),
                Fill = new SolidColorBrush(Color.Parse(accentColor)),
            };
            Grid.SetColumn(icon, 0);
            contentGrid.Children.Add(icon);

            var textPanel = new StackPanel { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Spacing = 2 };
            var titleText = new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, FontSize = 16, Margin = new Thickness(0, 2) };
            titleText.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextFillColorPrimaryBrush"));
            var subtitleText = new TextBlock { Text = subtitle, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2) };
            subtitleText.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextFillColorSecondaryBrush"));
            textPanel.Children.Add(titleText);
            textPanel.Children.Add(subtitleText);
            Grid.SetColumn(textPanel, 1);
            contentGrid.Children.Add(textPanel);

            var closeButton = new IconButton
            {
                Icon = LucideIconNames.X,
                IconSize = 12,
                CornerRadius = new CornerRadius(0, 10, 10, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Margin = new Thickness(20, 0, 0, 0),
                Width = 50,
            };
            closeButton.Bind(IconButton.ForegroundProperty, new DynamicResourceExtension("TextFillColorSecondaryBrush"));
            Grid.SetColumn(closeButton, 2);
            contentGrid.Children.Add(closeButton);

            var transform = new TranslateTransform(500, 0)
            {
                Transitions =
                [
                    new DoubleTransition { Property = TranslateTransform.XProperty, Duration = TimeSpan.FromMilliseconds(350), Easing = new QuarticEaseOut() },
                    new DoubleTransition { Property = TranslateTransform.YProperty, Duration = TimeSpan.FromMilliseconds(300), Easing = new QuarticEaseOut() }
                ]
            };

            var notification = new Border
            {
                Margin = new Thickness(0, 15, 15, 0),
                MinWidth = 350,
                Height = 80,
                CornerRadius = new CornerRadius(10),
                RenderTransform = transform,
                Child = contentGrid,
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 10, OffsetY = 4, Color = Color.Parse("#40000000") }),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };
            notification.Bind(Border.BackgroundProperty, new DynamicResourceExtension("NotificationBackgroundColor"));

            var entry = new NotificationEntry { Element = notification, Transform = transform };

            void Dismiss() => DismissNotification(entry);

            closeButton.Click += (s, e) => { e.Handled = true; Dismiss(); };
            notification.PointerPressed += (s, e) => { if (e.Source is IconButton) return; Dismiss(); };

            _notifications.Insert(0, entry);
            notificationPanel.Children.Add(notification);

            while (_notifications.Count > 3)
                DismissNotification(_notifications[^1]);

            RepositionNotifications();

            var cts = new CancellationTokenSource();
            entry.TimeoutCts = cts;

            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(50);
                if (cts.IsCancellationRequested) return;
                transform.X = 0;

                await Task.Delay(timeout);
                if (!cts.IsCancellationRequested)
                    Dismiss();
            });
        }

        private async void DismissNotification(NotificationEntry entry)
        {
            if (!_notifications.Remove(entry)) return;

            entry.TimeoutCts?.Cancel();
            RepositionNotifications();

            entry.Transform.X = 500;
            await Task.Delay(350);

            var notificationPanel = this.FindControl<Panel>("NotificationPanel");
            if (notificationPanel != null && notificationPanel.Children.Contains(entry.Element))
                notificationPanel.Children.Remove(entry.Element);
        }

        private void RepositionNotifications()
        {
            for (var i = 0; i < _notifications.Count; i++)
                _notifications[i].Transform.Y = i * (80 + 15);
        }

        private static string ToCRLF(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace('\n', '\r');

        private void OnCodeChanged(object? sender, EventArgs e)
        {
            if (_isInitialLoad)
            {
                _isInitialLoad = false;
                return;
            }

            _viewModel.Code = UIXML.Text;
            _viewModel.CodeChanged = true;
        }

        private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_viewModel.CodeChanged)
                return;

            e.Cancel = true;

            var result = await Frontend.ShowMessageBox(
                string.Format(CultureInfo.InvariantCulture, Strings.CustomTheme_Editor_ConfirmSave, _viewModel.Name),
                MessageBoxImage.Information,
                MessageBoxButton.YesNoCancel
            );

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.SaveCommand.Execute(null);
                _viewModel.CodeChanged = false;
                this.Close();
            }
            else if (result == MessageBoxResult.No)
            {
                _viewModel.CodeChanged = false;
                this.Close();
            }
        }

        private (string, int) GetLineAndPosAtCaretPosition()
        {
            int offset = UIXML.CaretOffset - 1;
            if (offset < 0) return ("", 0);

            var lineObj = UIXML.Document.GetLineByOffset(UIXML.CaretOffset);
            string lineText = UIXML.Document.GetText(lineObj.Offset, lineObj.Length);
            int column = UIXML.CaretOffset - lineObj.Offset - 1;

            return (lineText, column);
        }

        public static string? GetElementAtCursor(string xml, int offset, bool onlyAllowInside = false)
        {
            if (offset <= 0) return null;
            if (offset > xml.Length) offset = xml.Length;

            int startIdx = xml.LastIndexOf('<', offset - 1);
            if (startIdx < 0) return null;

            if (startIdx + 1 < xml.Length && xml[startIdx + 1] == '/')
                startIdx++;

            int endIdx1 = xml.IndexOf(' ', startIdx);
            if (endIdx1 == -1) endIdx1 = int.MaxValue;

            int endIdx2 = xml.IndexOf('>', startIdx);
            if (endIdx2 == -1)
            {
                endIdx2 = int.MaxValue;
            }
            else
            {
                if (onlyAllowInside && endIdx2 < offset) return null;
                if (endIdx2 > 0 && xml[endIdx2 - 1] == '/') endIdx2--;
            }

            int endIdx = Math.Min(endIdx1, endIdx2);
            if (endIdx > startIdx && endIdx < int.MaxValue)
            {
                string element = xml.Substring(startIdx + 1, endIdx - startIdx - 1);
                return element.StartsWith("!--", StringComparison.Ordinal) ? null : element;
            }
            return null;
        }

        private string? GetElementAtCursorNoSpaces()
        {
            (string line, int pos) = GetLineAndPosAtCaretPosition();

            string curr = "";
            while (pos != -1)
            {
                char c = line[pos];
                if (c == ' ' || c == '\t')
                    return null;
                if (c == '<')
                    return curr;
                curr = c + curr;
                pos--;
            }

            return null;
        }

        private string? ShowAttributesForElementName()
        {
            (string line, int pos) = GetLineAndPosAtCaretPosition();
            int numSpeech = line.Count(x => x == '"');
            if (numSpeech % 2 == 0)
            {
                int count = 0;
                for (int i = pos + 1; i < line.Length; i++)
                {
                    if (line[i] == '"') count++;
                }
                if (count % 2 != 0) return null;
            }
            return GetElementAtCursor(UIXML.Text, UIXML.CaretOffset, true);
        }

        private void AddEndTag()
        {
            CloseCompletionWindow();
            if (UIXML.CaretOffset >= 2 && UIXML.Text[UIXML.CaretOffset - 2] == '<')
            {
                var elementName = GetElementAtCursor(UIXML.Text, UIXML.CaretOffset - 2);
                if (elementName != null)
                    UIXML.TextArea.Document.Insert(UIXML.CaretOffset, $"{elementName}>");
            }
            else
            {
                if (UIXML.CaretOffset < UIXML.Text.Length && UIXML.Text[UIXML.CaretOffset] == '>') return;
                if (ShowAttributesForElementName() != null)
                    UIXML.TextArea.Document.Insert(UIXML.CaretOffset, ">");
            }
        }

        private void OpenElementAutoComplete()
        {
            var data = CustomBootstrapperSchema.ElementInfo.Keys
                .Select(e => new ElementCompletionData(e)).Cast<ICompletionData>().ToList();
            ShowCompletionWindow(data);
        }

        private void OpenAttributeAutoComplete()
        {
            string? element = ShowAttributesForElementName();

            if (element == null || !CustomBootstrapperSchema.ElementInfo.TryGetValue(element, out var attributes))
            {
                CloseCompletionWindow();
                return;
            }

            var data = attributes
                .Select(a => new AttributeCompletionData(a.Key, () => OpenTypeValueAutoComplete(a.Value)))
                .Cast<ICompletionData>().ToList();
            ShowCompletionWindow(data);
        }

        private void OpenTypeValueAutoComplete(string typeName)
        {
            if (!CustomBootstrapperSchema.Types.TryGetValue(typeName, out var type) || type.Values == null)
                return;

            var data = type.Values.Select(v => new TypeValueCompletionData(v))
                .Cast<ICompletionData>().ToList();
            ShowCompletionWindow(data);
        }

        private void OpenPropertyElementAutoComplete()
        {
            string? element = GetElementAtCursorNoSpaces();

            if (element == null || !CustomBootstrapperSchema.PropertyElements.TryGetValue(element, out var properties))
            {
                CloseCompletionWindow();
                return;
            }

            var data = properties
                .Select(p => new TypeValueCompletionData(p))
                .Cast<ICompletionData>()
                .ToList();

            ShowCompletionWindow(data);
        }

        private void CloseCompletionWindow()
        {
            _completionWindow?.Close();
            _completionWindow = null;
        }

        private void ShowCompletionWindow(List<ICompletionData> completionData)
        {
            CloseCompletionWindow();
            if (completionData.Count == 0) return;

            _completionWindow = new CompletionWindow(UIXML.TextArea);
            foreach (var c in completionData)
                _completionWindow.CompletionList.CompletionData.Add(c);

            _completionWindow.Show();
            _completionWindow.Closed += (_, _) => _completionWindow = null;
        }

        private void OnCancelButtonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.Close();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _completionWindow?.Close();
                _completionWindow = null;

                foreach (var entry in _notifications)
                {
                    entry.TimeoutCts?.Cancel();
                    entry.TimeoutCts?.Dispose();
                    var parent = entry.Element.Parent as Panel;
                    parent?.Children.Remove(entry.Element);
                }
                _notifications.Clear();
            }

            _disposed = true;
        }
    }

    internal class ElementCompletionData(string text) : ICompletionData
    {
        public IImage? Image => null;
        public string Text { get; } = text;
        public object Content => Text;
        public object? Description => null;
        public double Priority => 0;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
            => textArea.Document.Replace(completionSegment, this.Text);
    }

    internal class AttributeCompletionData(string text, Action openValueAction) : ICompletionData
    {
        public IImage? Image => null;
        public string Text { get; } = text;
        public object Content => Text;
        public object? Description => null;
        public double Priority => 0;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, this.Text + "=\"\"");
            textArea.Caret.Offset -= 1;
            Dispatcher.UIThread.Post(openValueAction);
        }
    }

    internal class TypeValueCompletionData(string text) : ICompletionData
    {
        public IImage? Image => null;
        public string Text { get; } = text;
        public object Content => Text;
        public object? Description => null;
        public double Priority => 0;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
            => textArea.Document.Replace(completionSegment, this.Text);
    }
}