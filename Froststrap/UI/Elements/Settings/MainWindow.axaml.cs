using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Froststrap.UI.Elements.Controls;
using Froststrap.UI.Elements.Settings;
using Froststrap.UI.ViewModels.Settings;
using IconPacks.Avalonia.Material;
using System.ComponentModel;

namespace Froststrap.UI.Elements.Settings
{
    public partial class MainWindow : Base.AvaloniaWindow
    {
        private Models.Persistable.WindowState _state => App.State.Prop.SettingsWindow;
        private MainWindowViewModel? _viewModel;

        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(bool showAlreadyRunningWarning) : this()
        {
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;

            _viewModel.RequestSaveNoticeEvent += (_, _) => ShowSaveNotification();
            _viewModel.RequestCloseWindowEvent += (_, _) => Close();

            App.Logger.WriteLine("MainWindow", "Initializing settings window");

            if (showAlreadyRunningWarning)
                ShowAlreadyRunningNotification();

            gbs.Opacity = _viewModel.GBSEnabled ? 1 : 0.5;
            gbs.IsEnabled = _viewModel.GBSEnabled; // binding doesnt work as expected so we are setting it in here instead

            LoadState();

            App.RemoteData.Subscribe((object? sender, EventArgs e) => {
                Dispatcher.UIThread.Post(() => {
                    RemoteDataBase Data = App.RemoteData.Prop;

                    if (AlertBar != null)
                    {
                        AlertBar.IsVisible = Data.AlertEnabled;
                        AlertBar.Message = Data.AlertContent;
                        AlertBar.Severity = Data.AlertSeverity;
                    }
                });
            });

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            this.Closing += MainWindow_Closing;
            this.Closed += MainWindow_Closed;

            App.WindowsBackdrop();

            UpdatePageView(_viewModel.CurrentPage);
            UpdateSelectedButtonStyle(_viewModel.SelectedPage);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_viewModel == null) return;

            if (e.PropertyName == nameof(MainWindowViewModel.CurrentPage))
            {
                UpdatePageView(_viewModel.CurrentPage);
            }
            else if (e.PropertyName == nameof(MainWindowViewModel.SelectedPage))
            {
                UpdateSelectedButtonStyle(_viewModel.SelectedPage);
            }
        }

        private void UpdatePageView(object? viewModel)
        {
            var pageControl = this.FindControl<TransitioningContentControl>("PageContentControl");
            if (pageControl == null || viewModel == null) return;

            var view = ResolveViewForViewModel(viewModel);

            if (view != null)
            {
                view.DataContext = viewModel;
                pageControl.Content = view;
            }
        }

        private void UpdateSelectedButtonStyle(string selectedPage)
        {
            var mainGrid = this.FindControl<Grid>("MainGrid");
            StackPanel? sidebarStack = null;

            if (mainGrid != null)
            {
                if (mainGrid.Children.FirstOrDefault() is Border sidebarBorder &&
                    sidebarBorder.Child is ScrollViewer sv && sv.Content is StackPanel sp)
                {
                    sidebarStack = sp;
                }
            }
            else
            {
                var border = this.GetVisualDescendants().OfType<Border>().FirstOrDefault();
                if (border?.Child is ScrollViewer scrollViewer && scrollViewer.Content is StackPanel stackPanel)
                {
                    sidebarStack = stackPanel;
                }
            }

            if (sidebarStack != null)
            {
                UpdateButtonStyles(sidebarStack, selectedPage);
            }
        }

        private void UpdateButtonStyles(StackPanel stackPanel, string selectedPage)
        {
            var accentFgKey = "AccentButtonBackground";
            var unselectedFgResource = "TextFillColorTertiaryBrush";
            var highlightBgResource = "ControlFillColorSecondaryBrush";

            foreach (var child in stackPanel.Children)
            {
                if (child is IconButton button && button.Tag is string tag)
                {
                    var isSelected = tag == selectedPage;

                    if (isSelected)
                    {
                        if (!button.Classes.Contains("Selected"))
                            button.Classes.Add("Selected");

                        button[!IconButton.BackgroundProperty] = button.GetResourceObservable(highlightBgResource).ToBinding();
                        button[!IconButton.ForegroundProperty] = button.GetResourceObservable(accentFgKey).ToBinding();
                    }
                    else
                    {
                        button.Classes.Remove("Selected");
                        button.Background = Brushes.Transparent;

                        button[!IconButton.ForegroundProperty] = button.GetResourceObservable(unselectedFgResource).ToBinding();
                    }
                }
            }
        }

        private Control? ResolveViewForViewModel(object viewModel)
        {
            var viewModelName = viewModel.GetType().Name;
            var viewName = viewModelName.Replace("ViewModel", "");

            var viewTypeNames = new[]
            {
                $"Froststrap.UI.Elements.Settings.Pages.FastFlags.{viewName}",
                $"Froststrap.UI.Elements.Settings.Pages.Mods.{viewName}Page",
                $"Froststrap.UI.Elements.Settings.Pages.{viewName}Page",
                $"Froststrap.UI.Elements.Settings.Pages.{viewName}",
                $"Froststrap.UI.Elements.Settings.{viewName}Page",
                $"Froststrap.UI.Elements.Settings.{viewName}"
            };

            foreach (var viewTypeName in viewTypeNames)
            {
                var viewType = Type.GetType(viewTypeName) ??
                               System.Reflection.Assembly.GetExecutingAssembly().GetType(viewTypeName);

                if (viewType != null && typeof(Control).IsAssignableFrom(viewType))
                {
                    try
                    {
                        return Activator.CreateInstance(viewType) as Control;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine("MainWindow", $"Failed to create view {viewTypeName}: {ex.Message}");
                    }
                }
            }

            return null;
        }

        public void LoadState()
        {
            var screen = Screens.Primary?.Bounds;
            if (screen != null)
            {
                if (_state.Left > screen.Value.Width) _state.Left = 0;
                if (_state.Top > screen.Value.Height) _state.Top = 0;
            }

            if (_state.Width > 0) this.Width = _state.Width;
            if (_state.Height > 0) this.Height = _state.Height;

            if (_state.Left > 0 && _state.Top > 0)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Position = new PixelPoint((int)_state.Left, (int)_state.Top);
            }
        }

        private void ShowSaveNotification()
        {
            ShowNotification(
                Strings.Menu_SettingsSaved_Title,
                Strings.Menu_SettingsSaved_Message,
                NotificationType.Success,
                3000);
        }

        private async void ShowAlreadyRunningNotification()
        {
            await Task.Delay(500);
            ShowNotification(
                Strings.Menu_AlreadyRunning_Title,
                Strings.Menu_AlreadyRunning_Caption,
                NotificationType.Warning,
                5000);
        }

        private void ShowNotification(string title, string subtitle, NotificationType type, int timeout)
        {
            var notificationPanel = this.FindControl<Panel>("NotificationPanel");
            if (notificationPanel == null) return;

            var accentColor = type == NotificationType.Success ? "#00D084" : "#FFB900";
            var iconKind = type == NotificationType.Success
                ? PackIconMaterialKind.CheckboxMultipleMarkedCircleOutline
                : PackIconMaterialKind.AlertOutline;

            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Margin = new Thickness(0)
            };

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
            textPanel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, FontSize = 14, Foreground = Brushes.White });
            textPanel.Children.Add(new TextBlock { Text = subtitle, FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(textPanel, 1);
            contentGrid.Children.Add(textPanel);

            var notification = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
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
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 15, OffsetY = 8, Color = Color.Parse("#60000000") })
            };

            notification.Transitions = new Transitions
            {
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

            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(50);
                notification.Opacity = 1;
                notification.RenderTransform = new TranslateTransform(0, 0);

                await Task.Delay(timeout);
                Dismiss();
            });
        }

        public void ShowLoading(string message = "Loading...")
        {
            var loadingOverlay = this.FindControl<Grid>("LoadingOverlay");
            var loadingText = this.FindControl<TextBlock>("LoadingOverlayText");

            if (loadingOverlay != null && loadingText != null)
            {
                loadingText.Text = message;
                loadingOverlay.IsVisible = true;
            }
        }

        public void HideLoading()
        {
            var loadingOverlay = this.FindControl<Grid>("LoadingOverlay");
            if (loadingOverlay != null)
            {
                loadingOverlay.IsVisible = false;
            }
        }

        #region Event Handlers

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _state.Width = this.Width;
            _state.Height = this.Height;
            _state.Left = this.Position.X;
            _state.Top = this.Position.Y;
        }

        private void MainWindow_Closed(object? sender, EventArgs e) => App.Logger.WriteLine("MainWindow", "Settings window closed");

        #endregion
    }
}
