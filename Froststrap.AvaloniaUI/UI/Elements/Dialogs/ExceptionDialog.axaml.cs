using Avalonia.Controls;
using System.Web;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class ExceptionDialog : Base.AvaloniaWindow
    {
        const int MAX_GITHUB_URL_LENGTH = 8192;

        public ExceptionDialog(Exception exception)
        {
            InitializeComponent();

            App.FrostRPC?.SetDialog("Exception");

            AddException(exception);

            if (!App.Logger.Initialized)
                LocateLogFileButton.Content = Strings.Dialog_Exception_CopyLogContents;

            string repoUrl = $"https://github.com/{App.ProjectRepository}";
            string wikiUrl = $"{repoUrl}/wiki";

            string title = HttpUtility.UrlEncode($"[BUG] {exception.GetType()}: {exception.Message}");
            string log = HttpUtility.UrlEncode(App.Logger.AsDocument);

            string issueUrl = $"{repoUrl}/issues/new?template=bug_report.yaml&title={title}&log={log}";

            if (issueUrl.Length > MAX_GITHUB_URL_LENGTH)
            {
                // url is way too long for github. remove the log parameter.
                issueUrl = $"{repoUrl}/issues/new?template=bug_report.yaml&title={title}";

                if (issueUrl.Length > MAX_GITHUB_URL_LENGTH)
                    issueUrl = $"{repoUrl}/issues/new?template=bug_report.yaml"; // bruh
            }

            string helpMessage = String.Format(Strings.Dialog_Exception_Info_2, wikiUrl, issueUrl);

            if (!App.IsActionBuild && !App.BuildMetadata.Machine.Contains("pizzaboxer", StringComparison.Ordinal))
                helpMessage = String.Format(Strings.Dialog_Exception_Info_2_Alt, wikiUrl);

            HelpMessageMarkdown.Markdown = helpMessage;
            VersionText.Text = String.Format(Strings.Dialog_Exception_Version, App.Version);

            ReportExceptionButton.Click += (_, _) => Utilities.ShellExecute(issueUrl);

            LocateLogFileButton.Click += async delegate
            {
                if (App.Logger.Initialized && !String.IsNullOrEmpty(App.Logger.FileLocation))
                {
                    Utilities.ShellExecute(App.Logger.FileLocation);
                }
                else
                {
                    // Copy to clipboard
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel?.Clipboard != null)
                    {
                        await topLevel.Clipboard.SetTextAsync(App.Logger.AsDocument);
                    }
                }
            };

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