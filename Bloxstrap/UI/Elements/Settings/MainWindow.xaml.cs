using Bloxstrap.UI.Elements.Settings.Pages;
using Bloxstrap.UI.ViewModels.Settings;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;

namespace Bloxstrap.UI.Elements.Settings
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INavigationWindow
    {
        private Models.Persistable.WindowState _state => App.State.Prop.SettingsWindow;

        public static ObservableCollection<NavigationItem> MainNavigationItems { get; } = new ObservableCollection<NavigationItem>();
        public static ObservableCollection<NavigationItem> FooterNavigationItems { get; } = new ObservableCollection<NavigationItem>();
        public ObservableCollection<NavigationItem> NavigationItemsView { get; } = new ObservableCollection<NavigationItem>();

        public static List<string> DefaultNavigationOrder { get; private set; } = new();
        public static List<string> DefaultFooterOrder { get; private set; } = new();

        public MainWindow(bool showAlreadyRunningWarning)
        {
            var viewModel = new MainWindowViewModel();

            viewModel.RequestSaveNoticeEvent += (_, _) => SettingsSavedSnackbar.Show();
            viewModel.RequestCloseWindowEvent += (_, _) => Close();

            DataContext = viewModel;

            InitializeComponent();

            App.Logger.WriteLine("MainWindow", "Initializing settings window");

            if (showAlreadyRunningWarning)
                ShowAlreadyRunningSnackbar();

            gbs.Opacity = viewModel.GBSEnabled ? 1 : 0.5;
            gbs.IsEnabled = viewModel.GBSEnabled; // binding doesnt work as expected so we are setting it in here instead

            LoadState();

            string? lastPageName = App.State.Prop.LastPage;
            Type? lastPage = lastPageName is null ? null : Type.GetType(lastPageName);

            App.RemoteData.Subscribe((object? sender, EventArgs e) => {
                RemoteDataBase Data = App.RemoteData.Prop;

                AlertBar.Visibility = Data.AlertEnabled ? Visibility.Visible : Visibility.Collapsed;
                AlertBar.Message = Data.AlertContent;
                AlertBar.Severity = Data.AlertSeverity;
            });

            App.WindowsBackdrop();

            var allItems = RootNavigation.Items.OfType<NavigationItem>().ToList();
            var allFooters = RootNavigation.Footer?.OfType<NavigationItem>().ToList() ?? new List<NavigationItem>();

            MainNavigationItems.Clear();
            foreach (var item in allItems)
                MainNavigationItems.Add(item);

            FooterNavigationItems.Clear();
            foreach (var item in allFooters)
                FooterNavigationItems.Add(item);

            CacheDefaultNavigationOrder();
            ReorderNavigationItemsFromSettings();
            RebuildNavigationItems();

            if (lastPage != null)
                SafeNavigate(lastPage);
            else
                RootNavigation.SelectedPageIndex = 0;

            RootNavigation.Navigated += OnNavigation!;

            void OnNavigation(object? sender, RoutedNavigationEventArgs e)
            {
                INavigationItem? currentPage = RootNavigation.Current;
                App.State.Prop.LastPage = currentPage?.PageType.FullName!;
            }
        }

        private async void SafeNavigate(Type page)
        {
            await Task.Delay(500); // ensure page service is ready

            if (page == typeof(RobloxSettingsPage) && !App.GlobalSettings.Loaded)
                return; // prevent from navigating onto disabled page

            Navigate(page);
        }

        public void LoadState()
        {
            if (_state.Left > SystemParameters.VirtualScreenWidth)
                _state.Left = 0;

            if (_state.Top > SystemParameters.VirtualScreenHeight)
                _state.Top = 0;

            if (_state.Width > 0)
                this.Width = _state.Width;

            if (_state.Height > 0)
                this.Height = _state.Height;

            if (_state.Left > 0 && _state.Top > 0)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = _state.Left;
                this.Top = _state.Top;
            }
        }

        private async void ShowAlreadyRunningSnackbar()
        {
            await Task.Delay(500); // wait for everything to finish loading
            AlreadyRunningSnackbar.Show();
        }

        #region Navigation reorder & persistence helpers

        private void CacheDefaultNavigationOrder()
        {
            DefaultNavigationOrder = MainNavigationItems
                .Select(x => x.Tag?.ToString() ?? string.Empty)
                .ToList();

            DefaultFooterOrder = FooterNavigationItems
                .Select(x => x.Tag?.ToString() ?? string.Empty)
                .ToList();
        }

        private void RebuildNavigationItems()
        {
            RootNavigation.Items.Clear();
            foreach (var item in MainNavigationItems)
                RootNavigation.Items.Add(item);

            if (RootNavigation.Footer == null)
                RootNavigation.Footer = new ObservableCollection<INavigationControl>();

            RootNavigation.Footer.Clear();
            foreach (var footerItem in FooterNavigationItems)
                RootNavigation.Footer.Add(footerItem);

            NavigationItemsView.Clear();
            foreach (var item in MainNavigationItems)
                NavigationItemsView.Add(item);
        }


        public void ApplyNavigationReorder()
        {
            RebuildNavigationItems();

            var order = MainNavigationItems
                .Concat(FooterNavigationItems)
                .Select(item => item.Tag?.ToString() ?? string.Empty)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            App.Settings.Prop.NavigationOrder = order;
        }

        private void ReorderNavigationItemsFromSettings()
        {
            if (App.Settings.Prop.NavigationOrder == null || App.Settings.Prop.NavigationOrder.Count == 0)
                return;

            var allItems = MainNavigationItems.Concat(FooterNavigationItems).ToList();

            var reorderedMain = new List<NavigationItem>();
            var reorderedFooter = new List<NavigationItem>();

            foreach (var tag in App.Settings.Prop.NavigationOrder)
            {
                var navItem = allItems.FirstOrDefault(i => i.Tag?.ToString() == tag);
                if (navItem != null)
                {
                    if (MainNavigationItems.Contains(navItem))
                        reorderedMain.Add(navItem);
                    else if (FooterNavigationItems.Contains(navItem))
                        reorderedFooter.Add(navItem);
                }
            }

            foreach (var item in MainNavigationItems)
            {
                if (!reorderedMain.Contains(item))
                    reorderedMain.Add(item);
            }
            foreach (var item in FooterNavigationItems)
            {
                if (!reorderedFooter.Contains(item))
                    reorderedFooter.Add(item);
            }

            MainNavigationItems.Clear();
            foreach (var item in reorderedMain)
                MainNavigationItems.Add(item);

            FooterNavigationItems.Clear();
            foreach (var item in reorderedFooter)
                FooterNavigationItems.Add(item);
        }

        public void ResetNavigationToDefault()
        {
            var available = RootNavigation.Items.OfType<NavigationItem>()
                .Concat(RootNavigation.Footer?.OfType<NavigationItem>() ?? Enumerable.Empty<NavigationItem>())
                .ToList();

            var reorderedMain = new List<NavigationItem>();
            var reorderedFooter = new List<NavigationItem>();

            foreach (var tag in DefaultNavigationOrder)
            {
                var navItem = available.FirstOrDefault(i => i.Tag?.ToString() == tag);
                if (navItem != null)
                    reorderedMain.Add(navItem);
            }

            foreach (var tag in DefaultFooterOrder)
            {
                var navItem = available.FirstOrDefault(i => i.Tag?.ToString() == tag);
                if (navItem != null)
                    reorderedFooter.Add(navItem);
            }

            foreach (var item in available)
            {
                if (!reorderedMain.Contains(item) && !reorderedFooter.Contains(item))
                    reorderedMain.Add(item);
            }

            MainNavigationItems.Clear();
            foreach (var item in reorderedMain)
                MainNavigationItems.Add(item);

            FooterNavigationItems.Clear();
            foreach (var item in reorderedFooter)
                FooterNavigationItems.Add(item);

            RebuildNavigationItems();

            App.Settings.Prop.NavigationOrder.Clear();
        }

        public int MoveNavigationItem(NavigationItem item, int direction)
        {
            if (item == null) return -1;

            if (!MainNavigationItems.Contains(item))
                return -1;

            var container = MainNavigationItems;

            int index = container.IndexOf(item);
            int newIndex = index + direction;

            if (newIndex < 0 || newIndex >= container.Count) return -1;

            container.Move(index, newIndex);
            ApplyNavigationReorder();

            return newIndex;
        }

        #endregion Navigation reorder & persistence helpers

        #region INavigationWindow methods

        public Frame GetFrame() => RootFrame;

        public INavigation GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(IPageService pageService) => RootNavigation.PageService = pageService;

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion INavigationWindow methods

        private void WpfUiWindow_Closing(object sender, CancelEventArgs e)
        {
            if (App.FastFlags.Changed || App.PendingSettingTasks.Any())
            {
                var result = Frontend.ShowMessageBox(Strings.Menu_UnsavedChanges, MessageBoxImage.Warning, MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                    e.Cancel = true;
            }

            _state.Width = this.Width;
            _state.Height = this.Height;

            _state.Top = this.Top;
            _state.Left = this.Left;

            App.State.Save();
        }

        private void WpfUiWindow_Closed(object sender, EventArgs e)
        {
            if (App.LaunchSettings.TestModeFlag.Active)
                LaunchHandler.LaunchRoblox(LaunchMode.Player);
            else
                App.SoftTerminate();
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = LaunchButton.ContextMenu;
            menu.PlacementTarget = LaunchButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Center;
            menu.VerticalOffset = 50;
            menu.IsOpen = true;
        }

        public void ShowLoading(string message = "Loading...")
        {
            LoadingOverlayText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        public void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }
}