using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Froststrap.Resources;
using Froststrap.UI.ViewModels.Settings;
using LucideAvalonia;
using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;

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

            App.RemoteData.Subscribe((object? sender, EventArgs e) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var data = App.RemoteData.Prop;
                    var alertText = this.FindControl<TextBlock>("AlertText");
                    if (alertText != null)
                    {
                        // Only show alert if AlertEnabled is true, otherwise show the CurrentPageTitle
                        if (data.AlertEnabled)
                        {
                            alertText.IsVisible = true;
                            alertText.Text = data.AlertContent;
                        }
                        else
                        {
                            alertText.IsVisible = true;
                            // Let the binding handle the text from CurrentPageTitle
                        }
                    }
                });
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
                    if (button.Content is LucideAvalonia.Lucide lucideIcon)
                    {
                        lucideIcon.StrokeBrush = isSelected ? selectedBrush : unselectedBrush;
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

        private async void ShowAlreadyRunningNotification()
        {
            await Task.Delay(500);
            ShowNotification(Strings.Menu_AlreadyRunning_Title, Strings.Menu_AlreadyRunning_Caption);
        }

        private async void ShowSaveNotification()
        {
            ShowNotification(Strings.Menu_SettingsSaved_Title, Strings.Menu_SettingsSaved_Message);
            await Task.Delay(3000);
        }

        private void ShowNotification(string title, string subtitle)
        {
            var notificationPanel = this.FindControl<Panel>("NotificationPanel");
            if (notificationPanel != null)
            {
                var closeButton = new Button
                {
                    Content = (LucideAvalonia.Enum.LucideIconNames)Enum.Parse(typeof(LucideAvalonia.Enum.LucideIconNames), "X"),
                    Background = new SolidColorBrush(Colors.Transparent),
                    Foreground = new SolidColorBrush(Color.Parse("#FFFFFF")),
                    FontSize = 16,
                    Padding = new Thickness(8),
                    Margin = new Thickness(20, 0, 0, 0),
                    Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
                };

                var contentGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    Margin = new Thickness(0)
                };

                var checkmark = new LucideAvalonia.Lucide
                {
                    Icon = (LucideAvalonia.Enum.LucideIconNames)Enum.Parse(typeof(LucideAvalonia.Enum.LucideIconNames), "CircleCheckBig"),
                    Width = 24,
                    Height = 24,
                    StrokeBrush = new SolidColorBrush(Color.Parse("#00D084")),
                    StrokeThickness = 2,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Margin = new Thickness(16, 0, 12, 0)
                };
                Grid.SetColumn(checkmark, 0);
                contentGrid.Children.Add(checkmark);

                var textPanel = new StackPanel
                {
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Spacing = 2
                };
                var titleText = new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"))
                };
                var subtitleText = new TextBlock
                {
                    Text = subtitle,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.Parse("#CCCCCC"))
                };
                textPanel.Children.Add(titleText);
                textPanel.Children.Add(subtitleText);
                Grid.SetColumn(textPanel, 1);
                contentGrid.Children.Add(textPanel);

                Grid.SetColumn(closeButton, 2);
                contentGrid.Children.Add(closeButton);

                var notification = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
                    BackgroundSizing = BackgroundSizing.InnerBorderEdge,
                    Opacity = 0.85,
                    BorderBrush = new SolidColorBrush(Color.Parse("#333333")),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Padding = new Thickness(0, 12, 28, 12),
                    Margin = new Thickness(0),
                    Height = 90,
                    CornerRadius = new CornerRadius(12),
                    RenderTransform = new TranslateTransform(0, 100),
                    Child = contentGrid
                };

                var transitions = new Transitions();
                transitions.Add(new TransformOperationsTransition 
                { 
                    Property = Border.RenderTransformProperty, 
                    Duration = TimeSpan.FromMilliseconds(300)
                });
                notification.Transitions = transitions;

                notificationPanel.Children.Add(notification);

                closeButton.Click += (s, e) =>
                {
                    notification.RenderTransform = new TranslateTransform(0, 100);
                    Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await Task.Delay(300);
                        notificationPanel.Children.Remove(notification);
                    });
                };

                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    notification.RenderTransform = new TranslateTransform(0, 0);

                    await Task.Delay(4000);

                    notification.RenderTransform = new TranslateTransform(0, 100);

                    await Task.Delay(300);
                    notificationPanel.Children.Remove(notification);
                });
            }
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
