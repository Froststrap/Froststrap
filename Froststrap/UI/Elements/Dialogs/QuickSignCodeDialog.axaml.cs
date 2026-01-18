using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class QuickSignCodeDialog : Base.AvaloniaWindow
    {
        public bool SignInSuccessful { get; private set; }
        private DispatcherTimer? _autoCloseTimer;

        public QuickSignCodeDialog()
        {
            InitializeComponent();
            SignInSuccessful = false;

            SetOwnerForCentering();

            CodeBox.IsVisible = true;
            StatusText.Text = "Waiting for Quick Sign-In...\nThe app will close this window when sign-in completes.";
        }

        private void SetOwnerForCentering()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Owner = desktop.MainWindow;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        public void StartNewSignIn(string code)
        {
            SignInSuccessful = false;

            _autoCloseTimer?.Stop();
            _autoCloseTimer = null;

            CodeTextBox.Text = code ?? string.Empty;
            CodeBox.IsVisible = true;
            StatusText.Text = "Waiting for Quick Sign-In...\nCopy the code above and enter it in the Quick Sign-In Page.";

            if (!IsVisible)
            {
                Show();
            }

            Activate();
            Focus();
        }

        public void CompleteSignIn()
        {
            SignInSuccessful = true;
            StatusText.Text = "Login complete! Closing...";

            _autoCloseTimer = new DispatcherTimer();
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(1.5);
            _autoCloseTimer.Tick += (s, e) =>
            {
                _autoCloseTimer?.Stop();
                Close();
            };
            _autoCloseTimer.Start();
        }

        private async void Copy_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(CodeTextBox.Text ?? "");
                    StatusText.Text = "Code copied to clipboard!";

                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    timer.Tick += (s, args) =>
                    {
                        timer.Stop();
                        StatusText.Text = "Waiting for Quick Sign-In...\nCopy the code above and enter it in the Roblox app.";
                    };
                    timer.Start();
                }
            }
            catch
            {
                // Ignore clipboard errors
            }
        }

        private void Close_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }

        public void UpdateStatus(string status, string? accountName = null)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    switch (status)
                    {
                        case "Validated":
                            CompleteSignIn();
                            break;
                        case "Cancelled":
                            StatusText.Text = "Sign-in cancelled.";
                            break;
                        case "TimedOut":
                            StatusText.Text = "Sign-in timed out.";
                            break;
                        case "UserLinked":
                            StatusText.Text = "Device linked - approving sign-in...";
                            break;
                        default:
                            if (!string.IsNullOrEmpty(accountName))
                            {
                                StatusText.Text = $"{status} - {accountName}";
                            }
                            else if (!string.IsNullOrEmpty(status))
                            {
                                StatusText.Text = status;
                            }
                            break;
                    }
                }
                catch
                {
                    // ignore dispatcher errors
                }
            });
        }
    }
}