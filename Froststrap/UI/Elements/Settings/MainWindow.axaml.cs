using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using FluentIcons.Common;
using Froststrap.UI.Elements.Controls;
using Froststrap.UI.Utility;
using Froststrap.UI.ViewModels.Settings;
using System.ComponentModel;

namespace Froststrap.UI.Elements.Settings
{
    public partial class MainWindow : Base.AvaloniaWindow
    {
        public static MainWindow? Instance { get; private set; }

        private Models.Persistable.WindowState _state => App.State.Prop.SettingsWindow;
        private MainWindowViewModel? _viewModel;

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
        }

        public MainWindow(bool showAlreadyRunningWarning) : this()
        {
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;

            _viewModel.RequestSaveNoticeEvent += (_, _) => ShowSaveNotification();
            _viewModel.RequestCloseWindowEvent += (_, _) => Close();
            _viewModel.SearchBar.SearchResultSelected += (_, item) => OnSearchResultSelected(item);

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

            Dispatcher.UIThread.Post(() =>
            {
                UpdateSelectedNavigationViewItem(_viewModel.SelectedPage);
                AttachTitleBarButtons();
                BuildSearchIndex();
            }, DispatcherPriority.Loaded);
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
                UpdateSelectedNavigationViewItem(_viewModel.SelectedPage);
            }
        }

        private SearchBarItem? _pendingSearchScrollItem;

        private void OnSearchResultSelected(SearchBarItem item)
        {
            _pendingSearchScrollItem = item;

            if (_viewModel?.SelectedPage != item.PageTag)
            {
                // Navigation will trigger UpdatePageView, which will scroll to the item
                var action = GetNavigationAction(item.PageTag ?? "");
                action?.Invoke();
            }
            else
            {
                ScrollToSearchItem(item);
            }
        }

        private Action? GetNavigationAction(string pageTag)
        {
            return pageTag switch
            {
                "integrations" => () => _viewModel?.NavigateToIntegrationsCommand.Execute(null),
                "behaviour" => () => _viewModel?.NavigateToBehaviourCommand.Execute(null),
                "mods" => () => _viewModel?.NavigateToPresetModsCommand.Execute(null),
                "fastflags" => () => _viewModel?.NavigateToFastFlagsCommand.Execute(null),
                "appearance" => () => _viewModel?.NavigateToAppearanceCommand.Execute(null),
                "regionselector" => () => _viewModel?.NavigateToRegionSelectorCommand.Execute(null),
                "globalsettings" => () => _viewModel?.NavigateToGlobalSettingsCommand.Execute(null),
                "shortcuts" => () => _viewModel?.NavigateToShortcutsCommand.Execute(null),
                "channels" => () => _viewModel?.NavigateToChannelsCommand.Execute(null),
                _ => null
            };
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

                Dispatcher.UIThread.Post(() =>
                {
                    var pageTag = _viewModel?.SelectedPage ?? "";
                    IndexPage(view, pageTag);

                    if (_pendingSearchScrollItem != null)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            ScrollToSearchItem(_pendingSearchScrollItem);
                            _pendingSearchScrollItem = null;
                        }, DispatcherPriority.Render);
                    }
                }, DispatcherPriority.Background);
            }
        }

        private void NavView_ItemInvoked(object? sender, NavigationViewItemInvokedEventArgs e)
        {
            if (e.InvokedItemContainer is NavigationViewItem navItem && navItem.Tag is string tag)
            {
                if (tag == "about")
                {
                    _viewModel?.OpenAboutCommand.Execute(null);
                    return;
                }

                var action = GetNavigationAction(tag);
                action?.Invoke();
            }
        }

        private void UpdateSelectedNavigationViewItem(string selectedPage)
        {
            var navView = this.FindControl<NavigationView>("NavView");
            if (navView == null) return;

            foreach(var item in navView.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag is string tag)
                {
                    if (tag == selectedPage)
                    {
                        navView.SelectedItem = navItem;
                        return;
                    }
                }
            }
            foreach(var item in navView.FooterMenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag is string tag)
                {
                    if (tag == selectedPage)
                    {
                        navView.SelectedItem = navItem;
                        return;
                    }
                }
            }
        }

        private static Control? ResolveViewForViewModel(object viewModel)
        {
            var viewModelName = viewModel.GetType().Name;
            var viewName = viewModelName.Replace("ViewModel", "");

            var viewTypeNames = new[]
            {
                $"Froststrap.UI.Elements.Settings.Pages.GlobalSettings.{viewName}",
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
                InfoBarSeverity.Success,
                3000);
        }

        private async void ShowAlreadyRunningNotification()
        {
            await Task.Delay(500);
            ShowNotification(
                Strings.Menu_AlreadyRunning_Title,
                Strings.Menu_AlreadyRunning_Caption,
                InfoBarSeverity.Warning,
                5000);
        }

        public static void ShowGlobalNotification(string title, string subtitle, InfoBarSeverity type, int timeout = 3000, FluentIcons.Common.Symbol? icon = null)
        {
            Dispatcher.UIThread.Post(() => Instance?.ShowNotification(title, subtitle, type, timeout, icon));
        }

        public void ShowNotification(string title, string subtitle, InfoBarSeverity type, int timeout, FluentIcons.Common.Symbol? customIcon = null)
        {
            var notificationPanel = this.FindControl<Panel>("NotificationPanel");
            if (notificationPanel == null) return;

            var accentColor = type == InfoBarSeverity.Success ? "#00D084" : "#FFB900";
            var iconSymbol = customIcon ?? (type == InfoBarSeverity.Success
                ? FluentIcons.Common.Symbol.CheckmarkCircle
                : FluentIcons.Common.Symbol.Warning);

            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Margin = new Thickness(0)
            };

            var icon = new FluentIcons.Avalonia.Fluent.SymbolIcon
            {
                Symbol = iconSymbol,
                FontSize = 28,
                Foreground = new SolidColorBrush(Color.Parse(accentColor)),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 12, 0)
            };
            Grid.SetColumn(icon, 0);
            contentGrid.Children.Add(icon);

            var textPanel = new StackPanel { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Spacing = 2 };

            var titleText = new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, FontSize = 14 };
            titleText.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextFillColorPrimaryBrush"));

            var subtitleText = new TextBlock { Text = subtitle, FontSize = 12, TextWrapping = TextWrapping.Wrap };
            subtitleText.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextFillColorSecondaryBrush"));

            textPanel.Children.Add(titleText);
            textPanel.Children.Add(subtitleText);
            Grid.SetColumn(textPanel, 1);
            contentGrid.Children.Add(textPanel);

            var notification = new Border
            {
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
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 10, OffsetY = 4, Color = Color.Parse("#40000000") })
            };

            notification.Bind(Border.BackgroundProperty, new DynamicResourceExtension("SolidBackgroundFillColorBase"));


            notification.Transitions =
            [
                new TransformOperationsTransition { Property = Border.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(350), Easing = new QuarticEaseOut() },
                new DoubleTransition { Property = Border.OpacityProperty, Duration = TimeSpan.FromMilliseconds(250) }
            ];

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

        private void AttachTitleBarButtons()
        {
            var minimizeButton = this.FindControl<IconButton>("PART_MinimizeButton");
            var maximizeButton = this.FindControl<IconButton>("PART_MaximizeButton");
            var closeButton = this.FindControl<IconButton>("PART_CloseButton");

            minimizeButton?.Click += (s, e) =>
                {
                    this.WindowState = Avalonia.Controls.WindowState.Minimized;
                };

            maximizeButton?.Click += (s, e) =>
                {
                    this.WindowState = this.WindowState == Avalonia.Controls.WindowState.Maximized 
                        ? Avalonia.Controls.WindowState.Normal 
                        : Avalonia.Controls.WindowState.Maximized;
                };

            closeButton?.Click += (s, e) =>
                {
                    this.Close();
                };
        }

        private SearchIndexBuilder? _searchIndexBuilder;

        private void BuildSearchIndex()
        {
            if (_viewModel == null) return;

            _searchIndexBuilder = new SearchIndexBuilder();

            var pages = new List<(string PageTag, string PageTitle, object PageViewModel)>
            {
                ("integrations", "Integrations", new IntegrationsViewModel()),
                ("behaviour", "Behaviour", new BehaviourViewModel()),
                ("mods", "Preset Mods", new ModsPresetsViewModel()),
                ("fastflags", "Fast Flags", new FastFlagsViewModel()),
                ("appearance", "Appearance", new AppearanceViewModel()),
                ("regionselector", "Region Selector", new RegionSelectorViewModel()),
                ("globalsettings", "Global Settings", new GlobalSettingsViewModel()),
                ("shortcuts", "Shortcuts", new ShortcutsViewModel()),
                ("channels", "Channels", new ChannelViewModel()),
            };

            var searchIndex = _searchIndexBuilder.BuildIndex(pages);

            var navigationActions = new Dictionary<string, Action>
            {
                { "integrations", () => _viewModel.NavigateToIntegrationsCommand.Execute(null) },
                { "behaviour", () => _viewModel.NavigateToBehaviourCommand.Execute(null) },
                { "mods", () => _viewModel.NavigateToPresetModsCommand.Execute(null) },
                { "fastflags", () => _viewModel.NavigateToFastFlagsCommand.Execute(null) },
                { "appearance", () => _viewModel.NavigateToAppearanceCommand.Execute(null) },
                { "regionselector", () => _viewModel.NavigateToRegionSelectorCommand.Execute(null) },
                { "globalsettings", () => _viewModel.NavigateToGlobalSettingsCommand.Execute(null) },
                { "shortcuts", () => _viewModel.NavigateToShortcutsCommand.Execute(null) },
                { "channels", () => _viewModel.NavigateToChannelsCommand.Execute(null) },
            };

            foreach (var item in searchIndex)
            {
                if (item.PageTag != null && navigationActions.TryGetValue(item.PageTag, out var action))
                {
                    item.NavigateAction = action;
                }
            }

            _viewModel.SearchBar.SetSearchIndex(searchIndex);

            PreIndexPages(pages);
        }

        private async void PreIndexPages(List<(string PageTag, string PageTitle, object PageViewModel)> pages)
        {
            var stagingArea = this.FindControl<Border>("OffscreenIndexingCanvas");
            if (stagingArea == null)
            {
                App.Logger.WriteLine("MainWindow::PreIndexPages", "OffscreenIndexingCanvas not found, skipping pre-index");
                return;
            }

            stagingArea.IsVisible = true;

            foreach (var (pageTag, _, pageViewModel) in pages)
            {
                try
                {
                    var view = ResolveViewForViewModel(pageViewModel);
                    if (view == null) continue;

                    view.DataContext = pageViewModel;
                    stagingArea.Child = view;

                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

                    IndexPage(view, pageTag);

                    stagingArea.Child = null;

                    // Small yield between pages to keep the UI responsive
                    await Task.Delay(30);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("MainWindow::PreIndexPages", $"Error pre-indexing page {pageTag}: {ex.Message}");
                }
            }

            stagingArea.IsVisible = false;
        }

        private void IndexPage(Control pageView, string pageTag)
        {
            if (_viewModel == null || _searchIndexBuilder == null) return;

            try
            {
                var addedItems = _searchIndexBuilder.ScanRenderedPageForElements(pageView, pageTag);

                if (addedItems.Count > 0)
                {
                var currentIndex = _viewModel.SearchBar.GetSearchIndex();
                    currentIndex.AddRange(addedItems);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("MainWindow::IndexPage", 
                    $"Error scanning page {pageTag}: {ex.Message}");
            }
        }

        private void ScrollToSearchItem(SearchBarItem item)
        {
            try
            {
                var pageControl = this.FindControl<TransitioningContentControl>("PageContentControl");
                if (pageControl?.Content is not Control pageView) return;

                if (!string.IsNullOrWhiteSpace(item.ParentSectionName))
                {
                    var parentExpander = pageView.GetVisualDescendants()
                        .OfType<CardExpander>()
                        .FirstOrDefault(ce => (ce.Header as string) == item.ParentSectionName);

                    if (parentExpander != null)
                    {
                        parentExpander.IsExpanded = true;
                    }
                }

                Control? targetControl = null;

                switch (item.Category)
                {
                    case "Section":
                        targetControl = pageView.GetVisualDescendants()
                            .OfType<CardExpander>()
                            .FirstOrDefault(ce => (ce.Header as string) == item.DisplayName);
                        break;

                    case "Setting":
                        targetControl = pageView.GetVisualDescendants()
                            .OfType<OptionControl>()
                            .FirstOrDefault(oc => oc.Header == item.DisplayName);
                        break;

                    case "Action":
                        targetControl = pageView.GetVisualDescendants()
                            .OfType<CardAction>()
                            .FirstOrDefault(ca => (ca.Content as string) == item.DisplayName);
                        break;

                    case "Label":
                        targetControl = pageView.GetVisualDescendants()
                            .OfType<TextBlock>()
                            .FirstOrDefault(tb => tb.Text == item.DisplayName);
                        break;
                }

                if (targetControl != null)
                {
                    targetControl.BringIntoView();
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("MainWindow::ScrollToSearchItem", 
                    $"Error scrolling to item: {ex.Message}");
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
            if (App.LaunchSettings.TestModeFlag.Active)
                LaunchHandler.LaunchRoblox(LaunchMode.Player);
            else
                App.SoftTerminate();

            App.Logger.WriteLine("MainWindow", "Settings window closed");
        }

        #endregion
    }
}
