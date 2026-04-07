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
using Avalonia.VisualTree;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.Elements.Settings;
using Froststrap.UI.ViewModels.Editor;
using Froststrap.UI.ViewModels.Settings;
using IconPacks.Avalonia.Material;
using System.ComponentModel;
using System.Xml;

namespace Froststrap.UI.Elements.Editor
{
    public partial class BootstrapperEditorWindow : AvaloniaWindow
    {
        private static class CustomBootstrapperSchema
        {
            private class Schema
            {
                public Dictionary<string, Element> Elements { get; set; } = new Dictionary<string, Element>();
                public Dictionary<string, Type> Types { get; set; } = new Dictionary<string, Type>();
            }

            private class Element
            {
                public string? SuperClass { get; set; } = null;
                public bool IsCreatable { get; set; } = false;
                public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
            }

            public class Type
            {
                public bool CanHaveElement { get; set; } = false;
                public List<string>? Values { get; set; } = null;
            }

            private static Schema? _schema;

            public static SortedDictionary<string, SortedDictionary<string, string>> ElementInfo { get; set; } = new();
            public static Dictionary<string, List<string>> PropertyElements { get; set; } = new();
            public static SortedDictionary<string, Type> Types { get; set; } = new();

            public static void ParseSchema()
            {
                if (_schema != null) return;

                try
                {
                    string json = Resource.GetString("CustomBootstrapperSchema.json").GetAwaiter().GetResult();
                    _schema = JsonSerializer.Deserialize<Schema>(json);

                    if (_schema == null) throw new Exception("Schema deserialization failed.");

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
                if (ElementInfo.ContainsKey(name))
                    return (ElementInfo[name], PropertyElements[name]);

                List<string> properties = new List<string>();
                SortedDictionary<string, string> attributes = new();

                foreach (var attribute in element.Attributes)
                {
                    attributes.Add(attribute.Key, attribute.Value);

                    if (!Types.ContainsKey(attribute.Value))
                        throw new Exception($"Schema for type {attribute.Value} is missing. Blame Matt!");

                    Type type = Types[attribute.Value];
                    if (type.CanHaveElement)
                        properties.Add(attribute.Key);
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
                List<string> toRemove = new List<string>();

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

        private BootstrapperEditorWindowViewModel _viewModel = null!;
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

            _viewModel = new BootstrapperEditorWindowViewModel();
            _viewModel.Directory = directory;
            _viewModel.Name = name;
            _viewModel.Code = ToCRLF(themeContents);
            _viewModel.Title = string.Format(Strings.CustomTheme_Editor_Title, name);

            DataContext = _viewModel;

            this.Loaded += (s, e) => {
                UIXML.Text = _viewModel.Code;
            };

            _viewModel.ThemeSavedCallback = (success, message) =>
            {
                if (success)
                {
                    Dispatcher.UIThread.Post(() => ShowSaveNotice());
                }
                else
                {
                    Dispatcher.UIThread.Post(() => ShowNotification("Error", message, NotificationType.Error, 5000));
                }
            };

            DataContext = _viewModel;

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

                using (Stream xmlStream = AssetLoader.Open(uri))
                using (XmlReader reader = XmlReader.Create(xmlStream))
                {
                    UIXML.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
            catch (Exception)
            {
                App.Logger.WriteLine("BootstrapperEditorWindow", $"Theme file not found, falling back to default XML.");
                UIXML.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
            }
        }

        private void ShowSaveNotice()
        {
            ShowNotification(
                Strings.Menu_SettingsSaved_Title,
                Strings.Menu_SettingsSaved_Message,
                NotificationType.Success,
                3000);
        }

        private void ShowNotification(string title, string subtitle, NotificationType type, int timeout)
        {
            var notificationPanel = this.FindControl<Panel>("NotificationPanel");
            if (notificationPanel == null) return;

            var accentColor = type == NotificationType.Success ? "#00D084" : "#FFB900";
            var iconKind = type == NotificationType.Success
                ? PackIconMaterialKind.CheckboxMultipleMarkedCircleOutline
                : PackIconMaterialKind.AlertOutline;

            var contentGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };

            var icon = new PackIconMaterial
            {
                Kind = iconKind,
                Width = 28,
                Height = 28,
                Foreground = new SolidColorBrush(Color.Parse(accentColor)),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 12, 0)
            };
            Grid.SetColumn(icon, 0);
            contentGrid.Children.Add(icon);

            var textPanel = new StackPanel { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Spacing = 2 };

            var titleText = new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, FontSize = 14 };
            titleText.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("SukiText"));

            var subtitleText = new TextBlock { Text = subtitle, FontSize = 12, TextWrapping = TextWrapping.Wrap };
            subtitleText.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("SukiLowText"));

            textPanel.Children.Add(titleText);
            textPanel.Children.Add(subtitleText);
            Grid.SetColumn(textPanel, 1);
            contentGrid.Children.Add(textPanel);

            var notification = new Border
            {
                BorderBrush = new SolidColorBrush(Color.Parse(accentColor)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 12, 24, 12),
                Margin = new Thickness(0, 0, 0, 10),
                MinWidth = 350,
                MaxWidth = 500,
                Height = 80,
                CornerRadius = new CornerRadius(12),
                Opacity = 0,
                RenderTransform = new TranslateTransform(0, 40),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = contentGrid,
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 10, OffsetY = 4, Color = Color.Parse("#40000000") })
            };

            notification.Bind(Border.BackgroundProperty, new DynamicResourceExtension("SolidBackgroundFillColorBase"));

            notification.Transitions = new Transitions {
                new TransformOperationsTransition { Property = Border.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(350), Easing = new QuarticEaseOut() },
                new DoubleTransition { Property = Border.OpacityProperty, Duration = TimeSpan.FromMilliseconds(250) }
            };

            async void Dismiss()
            {
                if (!notificationPanel.Children.Contains(notification)) return;
                notification.Opacity = 0;
                notification.RenderTransform = new TranslateTransform(0, 40);
                await Task.Delay(350);
                notificationPanel.Children.Remove(notification);
            }

            notification.PointerPressed += (s, e) => Dismiss();
            notificationPanel.Children.Add(notification);

            Dispatcher.UIThread.InvokeAsync(async () => {
                await Task.Delay(50);
                notification.Opacity = 1;
                notification.RenderTransform = new TranslateTransform(0, 0);
                await Task.Delay(timeout);
                Dismiss();
            });
        }

        private static string ToCRLF(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        }

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

        private string? GetElementAtCursorNoSpaces(string xml, int offset)
        {
            (string line, int pos) = GetLineAndPosAtCaretPosition();
            string curr = "";
            while (pos >= 0 && pos < line.Length)
            {
                char c = line[pos];
                if (c == ' ' || c == '\t') return null;
                if (c == '<') return curr;
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
            if (element == null || !CustomBootstrapperSchema.ElementInfo.ContainsKey(element))
            {
                CloseCompletionWindow();
                return;
            }

            var data = CustomBootstrapperSchema.ElementInfo[element]
                .Select(a => new AttributeCompletionData(a.Key, () => OpenTypeValueAutoComplete(a.Value)))
                .Cast<ICompletionData>().ToList();
            ShowCompletionWindow(data);
        }

        private void OpenTypeValueAutoComplete(string typeName)
        {
            var typeValues = CustomBootstrapperSchema.Types[typeName].Values;
            if (typeValues == null) return;

            var data = typeValues.Select(v => new TypeValueCompletionData(v))
                .Cast<ICompletionData>().ToList();
            ShowCompletionWindow(data);
        }

        private void OpenPropertyElementAutoComplete()
        {
            string? element = GetElementAtCursorNoSpaces(UIXML.Text, UIXML.CaretOffset);
            if (element == null || !CustomBootstrapperSchema.PropertyElements.ContainsKey(element))
            {
                CloseCompletionWindow();
                return;
            }

            var data = CustomBootstrapperSchema.PropertyElements[element]
                .Select(p => new TypeValueCompletionData(p)).Cast<ICompletionData>().ToList();
            ShowCompletionWindow(data);
        }

        private void CloseCompletionWindow()
        {
            if (_completionWindow != null)
            {
                _completionWindow.Close();
                _completionWindow = null;
            }
        }

        private void ShowCompletionWindow(List<ICompletionData> completionData)
        {
            CloseCompletionWindow();
            if (!completionData.Any()) return;

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

    public class ElementCompletionData : ICompletionData
    {
        public ElementCompletionData(string text) => Text = text;
        public IImage? Image => null;
        public string Text { get; }
        public object Content => Text;
        public object? Description => null;
        public double Priority => 0;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
            => textArea.Document.Replace(completionSegment, this.Text);
    }

    public class AttributeCompletionData : ICompletionData
    {
        private Action _openValueAction;
        public AttributeCompletionData(string text, Action openValueAction)
        {
            Text = text;
            _openValueAction = openValueAction;
        }
        public IImage? Image => null;
        public string Text { get; }
        public object Content => Text;
        public object? Description => null;
        public double Priority => 0;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, this.Text + "=\"\"");
            textArea.Caret.Offset -= 1;
            Dispatcher.UIThread.Post(_openValueAction);
        }
    }

    public class TypeValueCompletionData : ICompletionData
    {
        public TypeValueCompletionData(string text) => Text = text;
        public IImage? Image => null;
        public string Text { get; }
        public object Content => Text;
        public object? Description => null;
        public double Priority => 0;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
            => textArea.Document.Replace(completionSegment, this.Text);
    }
}