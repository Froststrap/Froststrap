using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Froststrap.UI.Elements.Base;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class QuickSignCodeDialog : AvaloniaWindow
    {
        public bool SignInSuccessful { get; private set; }
        private DispatcherTimer? _autoCloseTimer;

        public QuickSignCodeDialog()
        {
            InitializeComponent();
            SignInSuccessful = false;

            StatusText.Text = "Waiting for Quick Sign-In...\nThe app will close this window when sign-in completes.";
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

            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };

            _autoCloseTimer.Tick += (s, e) =>
            {
                _autoCloseTimer?.Stop();
                this.Close();
            };
            _autoCloseTimer.Start();
        }

        private async void Copy_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(CodeTextBox.Text);
                    StatusText.Text = "Code copied to clipboard!";

                    _ = Task.Delay(2000).ContinueWith(_ =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            StatusText.Text = "Waiting for Quick Sign-In...\nCopy the code above and enter it in the Roblox app.";
                        });
                    });
                }
            }
            catch
            {
                // Ignore clipboard errors
            }
        }

        private void Close_Click(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public void UpdateStatus(string status, string? accountName = null)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
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
            });
        }
    }
}