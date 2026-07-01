using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
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
using LucideAvalonia;
using LucideAvalonia.Enum;
using System.Xml;

namespace Froststrap.UI.Elements.Editor
{
    public partial class BootstrapperEditorWindow : AvaloniaWindow
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
                public string? SuperClass { get; set; } = null;
                public bool IsCreatable { get; set; } = false;
                public Dictionary<string, string> Attributes { get; set; } = [];
            }

            public class Type
            {
                public bool CanHaveElement { get; set; } = false;
                public List<string>? Values { get; set; } = null;
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
                    _schema = JsonSerializer.Deserialize<Schema>(json) ?? throw new Exception("Schema deserialization failed.");

                    foreach (var type in _schema.Types)
                        Types.Add(type.Key, type.Value);

                    PopulateElementInfo();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("Schema", $"Critical error loading schema: {ex.Message}");
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
                        throw new Exception($"Schema for type {attribute.Value} is missing. Blame Matt!");
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
        private CompletionWindow? _completionWindow = null;
        private bool _isInitialLoad = true;

        public BootstrapperEditorWindow()
        {
            InitializeComponent();
        }

        public BootstrapperEditorWindow(string name) : this()
        {
            CustomBootstrapperSchema.ParseSchema();

            string directory = Path.Combine(Paths.CustomThemes, name);
            string themeContents = File.ReadAllText(Path.Combine(directory, "Theme.xml"));

            _viewModel = new BootstrapperEditorWindowViewModel
            {
                Directory = directory,
                Name = name,
                Code = ToCRLF(themeContents),
                Title = string.Format(Strings.CustomTheme_Editor_Title, name)
            };

            DataContext = _viewModel;

            this.Loaded += (s, e) => {
                UIXML.Text = _viewModel.Code;
            };

            _viewModel.ThemeSavedCallback = (success, message) =>
            {
                if (success)
                {
                    Dispatcher.UIThread.Post(ShowSaveNotice);
                }
                else
                {
                    Dispatcher.UIThread.Post(() => ShowNotification("Error", message, InfoBarSeverity.Error, 5000));
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
                App.Logger.WriteLine("BootstrapperEditorWindow", "Theme file not found, falling back to default XML.");
                UIXML.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
            }
        }

        private Border? _currentNotification;
        private CancellationTokenSource? _notificationCts;
        private bool _isAnimatingOut = false;

        private void ShowSaveNotice()
        {
            ShowNotification(
                Strings.Menu_SettingsSaved_Title,
                Strings.Menu_SettingsSaved_Message,
                InfoBarSeverity.Success,
                3000);
        }

        public void ShowNotification(string title, string subtitle, InfoBarSeverity type, int timeout, LucideIconNames? customIcon = null)
        {
            var notificationPanel = this.FindControl<Panel>("NotificationPanel");
            if (notificationPanel == null) return;

            if (_isAnimatingOut)
            {
                Task.Run(async () =>
                {
                    while (_isAnimatingOut)
                    {
                        await Task.Delay(50);
                    }
                    Dispatcher.UIThread.Post(() => ShowNotification(title, subtitle, type, timeout, customIcon));
                });
                return;
            }

            _notificationCts?.Cancel();
            _notificationCts?.Dispose();
            _notificationCts = new CancellationTokenSource();
            var token = _notificationCts.Token;

            if (_currentNotification != null && notificationPanel.Children.Contains(_currentNotification))
            {
                _isAnimatingOut = true;
                var oldNotification = _currentNotification;

                oldNotification.Opacity = 0;
                oldNotification.RenderTransform = new TranslateTransform(0, 40);

                Task.Run(async () =>
                {
                    await Task.Delay(350);
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (notificationPanel.Children.Contains(oldNotification))
                        {
                            notificationPanel.Children.Remove(oldNotification);
                        }
                        _isAnimatingOut = false;
                        _currentNotification = null;

                        ShowNotificationInternal(title, subtitle, type, timeout, customIcon);
                    });
                });
                return;
            }

            ShowNotificationInternal(title, subtitle, type, timeout, customIcon);
        }

        private void ShowNotificationInternal(string title, string subtitle, InfoBarSeverity type, int timeout, LucideIconNames? customIcon = null)
        {
            var notificationPanel = this.FindControl<Panel>("NotificationPanel");
            if (notificationPanel == null) return;

            var accentColor = type == InfoBarSeverity.Success ? "#00D084" : "#FFB900";
            var iconSymbol = customIcon ?? (type == InfoBarSeverity.Success
                ? LucideIconNames.CircleCheck
                : LucideIconNames.TriangleAlert);

            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                Margin = new Thickness(0)
            };

            var icon = new Lucide
            {
                Icon = iconSymbol,
                Width = 28,
                Height = 28,
                StrokeBrush = new SolidColorBrush(Color.Parse(accentColor)),
                StrokeThickness = 2.5,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 12, 0)
            };
            Grid.SetColumn(icon, 0);
            contentGrid.Children.Add(icon);

            var textPanel = new StackPanel { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Spacing = 2 };

            var titleText = new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, FontSize = 14 };
            titleText.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextFillColorPrimaryBrush"));

            var subtitleText = new TextBlock { Text = subtitle, FontSize = 12, TextWrapping = TextWrapping.Wrap };
            subtitleText.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextFillColorSecondaryBrush"));

            textPanel.Children.Add(titleText);
            textPanel.Children.Add(subtitleText);
            Grid.SetColumn(textPanel, 1);
            contentGrid.Children.Add(textPanel);

            var closeButton = new IconButton
            {
                Icon = LucideIconNames.X,
                IconSize = 16,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Width = 32,
                Height = 32,
                Margin = new Thickness(0, 0, 12, 0)
            };

            closeButton.Bind(IconButton.ForegroundProperty, new DynamicResourceExtension("TextFillColorSecondaryBrush"));

            Grid.SetColumn(closeButton, 2);
            contentGrid.Children.Add(closeButton);

            var notification = new Border
            {
                BorderBrush = new SolidColorBrush(Color.Parse(accentColor)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 12, 0, 12),
                Margin = new Thickness(125, 0, 125, 40),
                MinWidth = 350,
                Height = 80,
                CornerRadius = new CornerRadius(6),
                Opacity = 0,
                RenderTransform = new TranslateTransform(0, 40),
                Child = contentGrid,
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 10, OffsetY = 4, Color = Color.Parse("#40000000") }),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
            };

            notification.Bind(Border.BackgroundProperty, new DynamicResourceExtension("NotificationBackgroundColor"));

            notification.Transitions =
            [
                new TransformOperationsTransition { Property = Border.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(350), Easing = new QuarticEaseOut() },
                new DoubleTransition { Property = Border.OpacityProperty, Duration = TimeSpan.FromMilliseconds(250) }
            ];

            async void Dismiss()
            {
                if (_notificationCts?.Token.IsCancellationRequested ?? false) return;
                if (!notificationPanel.Children.Contains(notification)) return;
                notification.Opacity = 0;
                notification.RenderTransform = new TranslateTransform(0, 40);
                await Task.Delay(350);
                if (notificationPanel.Children.Contains(notification))
                {
                    notificationPanel.Children.Remove(notification);
                }
                if (_currentNotification == notification)
                {
                    _currentNotification = null;
                }
            }

            closeButton.Click += (s, e) =>
            {
                e.Handled = true;
                Dismiss();
            };

            notification.PointerPressed += (s, e) =>
            {
                if (e.Source is IconButton) return;
                Dismiss();
            };

            _currentNotification = notification;
            notificationPanel.Children.Add(notification);

            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (_notificationCts?.Token.IsCancellationRequested ?? false) return;
                await Task.Delay(50);
                if (_notificationCts?.Token.IsCancellationRequested ?? false) return;
                notification.Opacity = 1;
                notification.RenderTransform = new TranslateTransform(0, 0);

                await Task.Delay(timeout);
                if (!(_notificationCts?.Token.IsCancellationRequested ?? false))
                {
                    Dismiss();
                }
            });
        }

        private static string ToCRLF(string text) => text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

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
                string.Format(Strings.CustomTheme_Editor_ConfirmSave, _viewModel.Name),
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
                return element.StartsWith("!--") ? null : element;
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
    }

    public class ElementCompletionData(string text) : ICompletionData
    {
        public IImage? Image => null;
        public string Text { get; } = text;
        public object Content => Text;
        public object? Description => null;
        public double Priority => 0;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
            => textArea.Document.Replace(completionSegment, this.Text);
    }

    public class AttributeCompletionData(string text, Action openValueAction) : ICompletionData
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

    public class TypeValueCompletionData(string text) : ICompletionData
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