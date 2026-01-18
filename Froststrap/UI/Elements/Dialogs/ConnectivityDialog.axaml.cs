using Avalonia.Media.Imaging;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class ConnectivityDialog : Base.AvaloniaWindow
    {
        public ConnectivityDialog(string title, string description, MessageBoxImage image, Exception exception)
        {
            InitializeComponent();

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
                IconImage.IsVisible = false;
            else
                IconImage.Source = new Bitmap($"avares://Froststrap/Resources/MessageBox/{iconFilename}.png");

            TitleTextBlock.Text = title;
            DescriptionMarkdownTextBlock.MarkdownText = description;

            AddException(exception);

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
                sb.AppendLine($"{exception.GetType()}: {exception.Message}");
            else
                sb.AppendLine($"[Inner Exception]\n{exception.GetType()}: {exception.Message}");

            if (exception.StackTrace != null)
                sb.AppendLine($"\nStack Trace:\n{exception.StackTrace}");

            if (exception.InnerException != null)
            {
                sb.AppendLine();
                AddExceptionToBuilder(exception.InnerException, sb, true);
            }

            ErrorTextBox.Text = sb.ToString();
        }

        private void AddExceptionToBuilder(Exception exception, StringBuilder sb, bool inner = false)
        {
            if (inner)
                sb.AppendLine($"[Inner Exception]\n{exception.GetType()}: {exception.Message}");
            else
                sb.AppendLine($"{exception.GetType()}: {exception.Message}");

            if (exception.StackTrace != null)
                sb.AppendLine($"\nStack Trace:\n{exception.StackTrace}");

            if (exception.InnerException != null)
            {
                sb.AppendLine();
                AddExceptionToBuilder(exception.InnerException, sb, true);
            }
        }
    }
}