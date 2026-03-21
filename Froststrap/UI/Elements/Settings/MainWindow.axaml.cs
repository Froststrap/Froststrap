using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Froststrap.UI.Elements.Controls;
using Froststrap.UI.ViewModels.Settings;
using IconPacks.Avalonia.Material;
using ReactiveUI;
using System.Reactive.Linq;

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
            var viewModel = new MainWindowViewModel();
            _viewModel = viewModel;

            viewModel.RequestSaveNoticeEvent += (_, _) => ShowSaveNotification();
            viewModel.RequestCloseWindowEvent += (_, _) => Close();

            DataContext = viewModel;

            App.Logger.WriteLine("MainWindow", "Initializing settings window");

            if (showAlreadyRunningWarning)
                ShowAlreadyRunningNotification();

            LoadState();

            App.RemoteData.Subscribe((object? sender, EventArgs e) => {
                RemoteDataBase Data = App.RemoteData.Prop;

                AlertBar.IsVisible = Data.AlertEnabled;
                AlertBar.Message = Data.AlertContent;
                AlertBar.Severity = Data.AlertSeverity;
            });

            App.WindowsBackdrop();

            viewModel.Router.CurrentViewModel
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(vm => UpdatePageView(vm));

            viewModel.WhenAnyValue(x => x.SelectedPage)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(page => UpdateSelectedButtonStyle(page));

            this.Closing += MainWindow_Closing;
            this.Closed += MainWindow_Closed;
        }

        private void UpdatePageView(IRoutableViewModel? viewModel)
        {
            var pageControl = this.FindControl<TransitioningContentControl>("PageContentControl");
            if (pageControl == null || viewModel == null) return;

            object dataContext = viewModel;

            var innerVmProp = viewModel.GetType().GetProperty("InnerViewModel");
            if (innerVmProp != null)
            {
                var inner = innerVmProp.GetValue(viewModel);
                if (inner != null)
                    dataContext = inner;
            }

            var view = ResolveViewForViewModel(dataContext);

            if (view != null)
            {
                view.DataContext = dataContext;
                pageControl.Content = view;
            }
        }

        private void UpdateSelectedButtonStyle(string selectedPage)
        {
            var mainGrid = this.FindControl<Grid>("MainGrid");
            if (mainGrid == null)
            {
                var border = this.VisualChildren.FirstOrDefault(c => c is Border b) as Border;
                if (border?.Child is ScrollViewer scrollViewer && scrollViewer.Content is StackPanel stackPanel)
                {
                    UpdateButtonStyles(stackPanel, selectedPage);
                }
                return;
            }

            if (mainGrid.Children.FirstOrDefault() is Border sidebarBorder && sidebarBorder.Child is ScrollViewer sv && sv.Content is StackPanel sp)
            {
                UpdateButtonStyles(sp, selectedPage);
            }
        }

        private void UpdateButtonStyles(StackPanel stackPanel, string selectedPage)
        {
            var unselectedBrush = new SolidColorBrush(Color.Parse("#888888"));
            var selectedBrush = new SolidColorBrush(Color.Parse("#00d4ff"));
            var highlightBgColor = new SolidColorBrush(Color.Parse("#333333"));

            foreach (var child in stackPanel.Children)
            {
                if (child is Button button && button.Tag is string tag)
                {
                    var isSelected = tag == selectedPage;

                    if (isSelected)
                    {
                        button.Background = highlightBgColor;
                        button.Foreground = selectedBrush;
                    }
                    else
                    {
                        button.Background = new SolidColorBrush(Colors.Transparent);
                        button.Foreground = unselectedBrush;
                    }

                    // Find and update the Lucide icon if it exists
                    if (button.Content is PackIconMaterial Icon)
                    {
                        Icon.Foreground = isSelected ? selectedBrush : unselectedBrush;
                    }
                }
            }
        }

        private Control? ResolveViewForViewModel(object viewModel)
        {
            var actualViewModelType = viewModel.GetType();

            var viewModelName = actualViewModelType.Name;
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

            // Try to find the type in all loaded assemblies
            foreach (var viewTypeName in viewTypeNames)
            {
                // First try Type.GetType
                var viewType = Type.GetType(viewTypeName);

                // If not found, search in loaded assemblies
                if (viewType == null)
                {
                    try
                    {
                        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                        viewType = assembly.GetType(viewTypeName, false);
                    }
                    catch { }
                }

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

            App.Logger.WriteLine("MainWindow", $"Could not find any view for {viewModelName}");
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

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            App.Logger.WriteLine("MainWindow", "Settings window closed");
        }

        #endregion
    }
}
