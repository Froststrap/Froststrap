using Avalonia.Controls;
using Avalonia.Input.Platform;
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

            StatusText.Text = Strings.Menu_QuickSignIn_Waitting;
        }

        public void StartNewSignIn(string code)
        {
            SignInSuccessful = false;

            _autoCloseTimer?.Stop();
            _autoCloseTimer = null;

            CodeTextBox.Text = code ?? string.Empty;
            CodeBox.IsVisible = true;
            StatusText.Text = Strings.Menu_QuickSignIn_Waitting;

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
            StatusText.Text = Strings.Menu_QuickSignIn_Complete;

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

                    _ = Task.Delay(2000).ContinueWith(_ =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            StatusText.Text = Strings.Menu_QuickSignIn_Waitting;
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
                        StatusText.Text = Strings.Menu_QuickSignIn_Cancelled;
                        break;
                    case "TimedOut":
                        StatusText.Text = Strings.Menu_QuickSignIn_TimedOut;
                        break;
                    case "UserLinked":
                        StatusText.Text = Strings.Menu_QuickSignIn_Linked;
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