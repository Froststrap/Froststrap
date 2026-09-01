using Avalonia.Controls;
using Avalonia.Input.Platform;
using System.Web;

namespace Froststrap.UI.Elements.Dialogs
{
    internal partial class ExceptionDialog : Base.AvaloniaWindow
    {
        const int MAX_GITHUB_URL_LENGTH = 8192;

        public ExceptionDialog()
        {
            InitializeComponent();
        }

        public ExceptionDialog(Exception exception) : this()
        {
            App.FrostRPC?.SetDialog("Exception");

            AddException(exception);

            if (!Logging.Initialized)
                LocateLogFileButton.Content = Strings.Dialog_Exception_CopyLogContents;

            string repoUrl = $"https://github.com/{App.ProjectRepository}";
            string wikiUrl = $"{repoUrl}/wiki";

            string title = HttpUtility.UrlEncode($"[BUG] {exception.GetType()}: {exception.Message}");
            string log = HttpUtility.UrlEncode(Logging.AsDocument);

            string issueUrl = $"{repoUrl}/issues/new?template=bug_report.yaml&title={title}&log={log}";

            // GUARD: Shorten url since too long
            if (issueUrl.Length > MAX_GITHUB_URL_LENGTH)
            {
                issueUrl = $"{repoUrl}/issues/new?template=bug_report.yaml&title={title}";

                // GUARD: Shorten url (again) since too long
                if (issueUrl.Length > MAX_GITHUB_URL_LENGTH)
                    issueUrl = $"{repoUrl}/issues/new?template=bug_report.yaml";
            }

            string helpMessage = String.Format(CultureInfo.InvariantCulture, Strings.Dialog_Exception_Info_2, wikiUrl, issueUrl);

            if (!App.IsActionBuild)
                helpMessage = String.Format(CultureInfo.InvariantCulture, Strings.Dialog_Exception_Info_2_Alt, wikiUrl);

            HelpMessageMarkdown.MarkdownText = helpMessage;
            VersionText.Text = String.Format(CultureInfo.InvariantCulture, Strings.Menu_About_Version, App.Version);

            ReportExceptionButton.Click += (_, _) => Utilities.ShellExecute(issueUrl);

            LocateLogFileButton.Click += async delegate
            {
                if (Logging.Initialized && !String.IsNullOrEmpty(Logging.FileLocation))
                {
                    Utilities.ShellExecute(Logging.FileLocation);
                }
                else
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel?.Clipboard != null)
                    {
                        await topLevel.Clipboard.SetTextAsync(Logging.AsDocument);
                    }
                }
            };

            CopyLogButton.Click += async delegate
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(Logging.AsDocument);
                }
            };

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
