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
using Froststrap.UI.Elements.Controls;
using Froststrap.UI.Utility;
using Froststrap.UI.ViewModels.Settings;
using Froststrap.UI.ViewModels.Settings.Mods;
using System.ComponentModel;
using Symbol = FluentIcons.Common.Symbol;

namespace Froststrap.UI.Elements.Settings
{
    public partial class MainWindow : Locale.LocaleAwareWindow
    {
        public static MainWindow? Instance { get; private set; }

        private static Models.Persistable.WindowState State => App.State.Prop.SettingsWindow;
        private readonly MainWindowViewModel? _viewModel;

        private Border? _currentNotification;
        private CancellationTokenSource? _notificationCts;
        private bool _isAnimatingOut = false;

        private readonly Dictionary<string, Control> _cachedPages = [];

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();

            if (Locale.RightToLeft)
            {
                this.FlowDirection = FlowDirection.RightToLeft;
            }
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

            sober.Opacity = _viewModel.SoberEnabled ? 1 : 0.5;
            sober.IsEnabled = _viewModel.SoberEnabled; // binding doesnt work as expected so we are setting it in here instead

            LoadState();

            App.RemoteData.Subscribe((_, _) => Dispatcher.UIThread.Post(() =>
            {
                var data = App.RemoteData.Prop;

                if (AlertBar is not null)
                {
                    AlertBar.IsVisible = data.AlertEnabled;
                    AlertBar.Message = data.AlertContent;
                    AlertBar.Severity = data.AlertSeverity;
                }
            }));

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            this.Closing += MainWindow_Closing;
            this.Closed += MainWindow_Closed;

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
                "sobersettings" => () => _viewModel?.NavigateToSoberSettingsCommand.Execute(null),
                "mods" => () => _viewModel?.NavigateToPresetModsCommand.Execute(null),
                "fastflags" => () => _viewModel?.NavigateToFastFlagsCommand.Execute(null),
                "appearance" => () => _viewModel?.NavigateToAppearanceCommand.Execute(null),
                "regionselector" => () => _viewModel?.NavigateToRegionSelectorCommand.Execute(null),
                "globalsettings" => () => _viewModel?.NavigateToGlobalSettingsCommand.Execute(null),
                "shortcuts" => () => _viewModel?.NavigateToShortcutsCommand.Execute(null),
                "quickplay" => () => _viewModel?.NavigateToQuickPlayCommand.Execute(null),
                "channels" => () => _viewModel?.NavigateToChannelsCommand.Execute(null),
                _ => null
            };
        }

        private readonly Dictionary<string, (string Title, Symbol Icon)> _pageInfo = new()
        {
            ["integrations"] = ("Integrations", Symbol.Link),
            ["behaviour"] = ("Behaviour", Symbol.PlaySettings),
            ["sobersettings"] = ("Sober Settings", Symbol.Settings),
            ["mods"] = ("Preset Mods", Symbol.PuzzlePiece),
            ["fastflags"] = ("Fast Flags", Symbol.Flag),
            ["appearance"] = ("Appearance", Symbol.Color),
            ["regionselector"] = ("Region Selector", Symbol.Earth),
            ["globalsettings"] = ("Global Settings", Symbol.EditSettings),
            ["shortcuts"] = ("Shortcuts", Symbol.Link),
            ["quickplay"] = ("Quick Play", Symbol.Replay),
            ["channels"] = ("Channels", Symbol.CloudArrowUp),
        };

        private void UpdatePageView(object? viewModel)
        {
            var pageControl = this.FindControl<TransitioningContentControl>("PageContentControl");
            if (pageControl == null || viewModel == null) return;

            bool shouldReinitialize = viewModel is AppearanceViewModel || viewModel is ModsViewModel;

            string pageTag = _viewModel?.SelectedPage ?? "";
            Control? view = null;

            if (!shouldReinitialize && !string.IsNullOrEmpty(pageTag) && _cachedPages.TryGetValue(pageTag, out var cachedView))
            {
                view = cachedView;
            }
            else
            {
                view = ResolveViewForViewModel(viewModel);

                if (view != null && !shouldReinitialize && !string.IsNullOrEmpty(pageTag))
                {
                    _cachedPages[pageTag] = view;
                }
            }

            if (view != null)
            {
                view.DataContext = viewModel;
                pageControl.Content = view;

                Dispatcher.UIThread.Post(() =>
                {
                    if (!string.IsNullOrEmpty(pageTag) && _pageInfo.TryGetValue(pageTag, out var info))
                    {
                        IndexPage(view, pageTag, info.Title, info.Icon);
                    }

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

            foreach (var item in navView.MenuItems)
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
            foreach (var item in navView.FooterMenuItems)
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
                if (State.Left > screen.Value.Width) State.Left = 0;
                if (State.Top > screen.Value.Height) State.Top = 0;
            }

            if (State.Width > 0) this.Width = State.Width;
            if (State.Height > 0) this.Height = State.Height;

            if (State.Left > 0 && State.Top > 0)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Position = new PixelPoint((int)State.Left, (int)State.Top);
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

        public static void ShowGlobalNotification(string title, string subtitle, InfoBarSeverity type, int timeout = 3000, Symbol? icon = null)
        {
            Dispatcher.UIThread.Post(() => Instance?.ShowNotification(title, subtitle, type, timeout, icon));
        }

        public void ShowNotification(string title, string subtitle, InfoBarSeverity type, int timeout, Symbol? customIcon = null)
        {
            var notificationPanel = this.FindControl<Panel>("NotificationPanel");
            if (notificationPanel == null) return;

            // If a notification is currently animating out, wait for it to finish
            if (_isAnimatingOut)
            {
                // Schedule this notification to show after the animation completes
                Task.Run(async () =>
                {
                    while (_isAnimatingOut)
                    {
                        await Task.Delay(50);
                    }
                    Dispatcher.UIThread.Post(() => ShowNotification(title, subtitle, type, timeout, customIcon));
                });
                return;
            }

            // Cancel existing notification timeout
            _notificationCts?.Cancel();
            _notificationCts?.Dispose();
            _notificationCts = new CancellationTokenSource();
            var token = _notificationCts.Token;

            // If there's a current notification, animate it out first
            if (_currentNotification != null && notificationPanel.Children.Contains(_currentNotification))
            {
                _isAnimatingOut = true;
                var oldNotification = _currentNotification;

                // Animate out the old notification
                oldNotification.Opacity = 0;
                oldNotification.RenderTransform = new TranslateTransform(0, 40);

                // Wait for animation to complete
                Task.Run(async () =>
                {
                    await Task.Delay(350);
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (notificationPanel.Children.Contains(oldNotification))
                        {
                            notificationPanel.Children.Remove(oldNotification);
                        }
                        _isAnimatingOut = false;
                        _currentNotification = null;

                        // Now show the new notification
                        ShowNotificationInternal(title, subtitle, type, timeout, customIcon);
                    });
                });
                return;
            }

            // No existing notification, show directly
            ShowNotificationInternal(title, subtitle, type, timeout, customIcon);
        }

        private void ShowNotificationInternal(string title, string subtitle, InfoBarSeverity type, int timeout, Symbol? customIcon = null)
        {
            var notificationPanel = this.FindControl<Panel>("NotificationPanel");
            if (notificationPanel == null) return;

            var accentColor = type == InfoBarSeverity.Success ? "#00D084" : "#FFB900";
            var iconSymbol = customIcon ?? (type == InfoBarSeverity.Success
                ? Symbol.CheckmarkCircle
                : Symbol.Warning);

            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
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

            var closeButton = new IconButton
            {
                Icon = Symbol.Dismiss,
                IconSize = 16,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Width = 32,
                Height = 32,
                Margin = new Thickness(0, 0, 12, 0)
            };

            closeButton.Bind(IconButton.ForegroundProperty, new DynamicResourceExtension("TextFillColorSecondaryBrush"));

            Grid.SetColumn(closeButton, 2);
            contentGrid.Children.Add(closeButton);

            var notification = new Border
            {
                BorderBrush = new SolidColorBrush(Color.Parse(accentColor)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 12, 0, 12),
                Margin = new Thickness(200, 0, 200, 40),
                MinWidth = 350,
                Height = 80,
                CornerRadius = new CornerRadius(6),
                Opacity = 0,
                RenderTransform = new TranslateTransform(0, 40),
                Child = contentGrid,
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 10, OffsetY = 4, Color = Color.Parse("#40000000") }),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
            };

            notification.Bind(Border.BackgroundProperty, new DynamicResourceExtension("NotificationBackgroundColor"));

            notification.Transitions =
            [
                new TransformOperationsTransition { Property = Border.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(350), Easing = new QuarticEaseOut() },
        new DoubleTransition { Property = Border.OpacityProperty, Duration = TimeSpan.FromMilliseconds(250) }
            ];

            async void Dismiss()
            {
                if (_notificationCts?.Token.IsCancellationRequested ?? false) return;
                if (!notificationPanel.Children.Contains(notification)) return;
                notification.Opacity = 0;
                notification.RenderTransform = new TranslateTransform(0, 40);
                await Task.Delay(350);
                if (notificationPanel.Children.Contains(notification))
                {
                    notificationPanel.Children.Remove(notification);
                }
                if (_currentNotification == notification)
                {
                    _currentNotification = null;
                }
            }

            closeButton.Click += (s, e) =>
            {
                e.Handled = true;
                Dismiss();
            };

            notification.PointerPressed += (s, e) =>
            {
                if (e.Source is IconButton) return;
                Dismiss();
            };

            _currentNotification = notification;
            notificationPanel.Children.Add(notification);

            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (_notificationCts?.Token.IsCancellationRequested ?? false) return;
                await Task.Delay(50);
                if (_notificationCts?.Token.IsCancellationRequested ?? false) return;
                notification.Opacity = 1;
                notification.RenderTransform = new TranslateTransform(0, 0);

                await Task.Delay(timeout);
                if (!(_notificationCts?.Token.IsCancellationRequested ?? false))
                {
                    Dismiss();
                }
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
            loadingOverlay?.IsVisible = false;
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
                ("sobersettings", "Sober Settings", new SoberSettingsViewModel()),
                ("mods", "Preset Mods", new ModsPresetsViewModel()),
                ("fastflags", "Fast Flags", new FastFlagsViewModel()),
                ("appearance", "Appearance", new AppearanceViewModel()),
                ("regionselector", "Region Selector", new RegionSelectorViewModel()),
                ("globalsettings", "Global Settings", new GlobalSettingsViewModel()),
                ("shortcuts", "Shortcuts", new ShortcutsViewModel()),
                ("quickplay", "Quick Play", new QuickPlayViewModel()),
                ("channels", "Channels", new ChannelViewModel()),
            };

            if (!_viewModel.GBSEnabled)
                pages.RemoveAll(p => p.PageTag == "globalsettings");
            if (!_viewModel.SoberEnabled)
                pages.RemoveAll(p => p.PageTag == "sobersettings");

            var searchIndex = _searchIndexBuilder.BuildIndex(pages);

            var navigationActions = new Dictionary<string, Action>
            {
                { "integrations", () => _viewModel.NavigateToIntegrationsCommand.Execute(null) },
                { "behaviour", () => _viewModel.NavigateToBehaviourCommand.Execute(null) },
                { "sobersettings", () => _viewModel.NavigateToSoberSettingsCommand.Execute(null) },
                { "mods", () => _viewModel.NavigateToPresetModsCommand.Execute(null) },
                { "fastflags", () => _viewModel.NavigateToFastFlagsCommand.Execute(null) },
                { "appearance", () => _viewModel.NavigateToAppearanceCommand.Execute(null) },
                { "regionselector", () => _viewModel.NavigateToRegionSelectorCommand.Execute(null) },
                { "globalsettings", () => _viewModel.NavigateToGlobalSettingsCommand.Execute(null) },
                { "shortcuts", () => _viewModel.NavigateToShortcutsCommand.Execute(null) },
                { "quickplay", () => _viewModel.NavigateToQuickPlayCommand.Execute(null) },
                { "channels", () => _viewModel.NavigateToChannelsCommand.Execute(null) },
            };

            foreach (var item in searchIndex)
            {
                if (item.PageTag != null && navigationActions.TryGetValue(item.PageTag, out var action))
                {
                    item.NavigateAction = action;
                }
            }

            _viewModel.SearchBar.SetSearchIndex([]);

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

            foreach (var (pageTag, pageTitle, pageViewModel) in pages)
            {
                try
                {
                    var view = ResolveViewForViewModel(pageViewModel);
                    if (view == null) continue;

                    view.DataContext = pageViewModel;
                    stagingArea.Child = view;

                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

                    var (_, icon) = _pageInfo[pageTag];
                    IndexPage(view, pageTag, pageTitle, icon);

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

        private void IndexPage(Control pageView, string pageTag, string pageTitle, Symbol pageIcon)
        {
            if (_viewModel == null || _searchIndexBuilder == null) return;

            try
            {
                var addedItems = _searchIndexBuilder.ScanRenderedPageForElements(pageView, pageTag);

                if (addedItems.Count > 0)
                {
                    foreach (var item in addedItems)
                    {
                        item.PageName = pageTitle;
                        item.IconSymbol = pageIcon;
                    }

                    var hiddenControlHeaders = pageView.GetVisualDescendants()
                        .Where(c => !c.IsVisible)
                        .Select(c => {
                            if (c is OptionControl oc) return oc.Header?.ToString();
                            if (c is CardExpander ce) return ce.Header?.ToString();
                            if (c is CardAction ca) return ca.Header?.ToString();
                            if (c is TextBlock tb) return tb.Text;
                            return null;
                        })
                        .Where(name => !string.IsNullOrEmpty(name))
                        .ToHashSet();

                    var filteredItems = addedItems
                        .Where(item => !hiddenControlHeaders.Contains(item.DisplayName))
                        .ToList();

                    if (filteredItems.Count > 0)
                    {
                        var currentIndex = _viewModel.SearchBar.GetSearchIndex();
                        currentIndex.AddRange(filteredItems);
                    }
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

                    parentExpander?.IsExpanded = true;
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

                targetControl?.BringIntoView();
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
            State.Width = this.Width;
            State.Height = this.Height;
            State.Left = this.Position.X;
            State.Top = this.Position.Y;

            if (_viewModel?.CurrentPage != null)
            {
                App.State.Prop.LastPage = _viewModel.CurrentPage.GetType().FullName;
                App.State.SaveSetting("LastPage");
            }
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