using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Froststrap.UI.Elements.Dialogs
{
    internal partial class ConnectivityDialog : Base.AvaloniaWindow
    {
        public ConnectivityDialog()
        {
            InitializeComponent();
        }

        public ConnectivityDialog(string title, string description, MessageBoxImage image, Exception exception) : this()
        {
            App.FrostRPC?.SetDialog("Connectivity");

            string? iconFilename = null;

            switch (image)
            {
                case MessageBoxImage.Error:
                    iconFilename = "Error";
                    break;

                case MessageBoxImage.Question:
                    iconFilename = "Question";
                    break;

                case MessageBoxImage.Warning:
                    iconFilename = "Warning";
                    break;

                case MessageBoxImage.Information:
                    iconFilename = "Information";
                    break;
            }

            if (iconFilename is null)
            {
                IconImage.IsVisible = false;
            }
            else
            {
                var uri = new Uri($"avares://Froststrap/Resources/MessageBox/{iconFilename}.png");
                using var stream = AssetLoader.Open(uri);
                IconImage.Source = new Bitmap(stream);
            }

            TitleTextBlock.Text = title;
            DescriptionMarkdownTextBlock.MarkdownText = description;

            AddException(exception);

            VersionText.Text = String.Format(CultureInfo.InvariantCulture, Strings.Menu_About_Version, App.Version);

            CloseButton.Click += (_, _) => Close();

            Loaded += (_, _) =>
            {
                Activate();
                Topmost = true;
                Topmost = false;
            };
        }

        private void AddException(Exception exception, bool inner = false)
        {
            var sb = new StringBuilder();

            if (!inner)
                sb.AppendLine(CultureInfo.InvariantCulture, $"{exception.GetType()}: {exception.Message}");
            else
                sb.AppendLine(CultureInfo.InvariantCulture, $"[Inner Exception]\n{exception.GetType()}: {exception.Message}");

            if (exception.StackTrace != null)
                sb.AppendLine(CultureInfo.InvariantCulture, $"\nStack Trace:\n{exception.StackTrace}");

            if (exception.InnerException != null)
            {
                sb.AppendLine();
                AddExceptionToBuilder(exception.InnerException, sb, true);
            }

            ErrorTextBox.Text = sb.ToString();
        }

        private static void AddExceptionToBuilder(Exception exception, StringBuilder sb, bool inner = false)
        {
            if (inner)
                sb.AppendLine(CultureInfo.InvariantCulture, $"[Inner Exception]\n{exception.GetType()}: {exception.Message}");
            else
                sb.AppendLine(CultureInfo.InvariantCulture, $"{exception.GetType()}: {exception.Message}");

            if (exception.StackTrace != null)
                sb.AppendLine(CultureInfo.InvariantCulture, $"\nStack Trace:\n{exception.StackTrace}");

            if (exception.InnerException != null)
            {
                sb.AppendLine();
                AddExceptionToBuilder(exception.InnerException, sb, true);
            }
        }
    }
}