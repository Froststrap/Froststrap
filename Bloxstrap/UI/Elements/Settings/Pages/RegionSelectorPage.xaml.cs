using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class RegionSelectorPage
    {
        private bool _windowBindingsAttached = false;

        public RegionSelectorPage()
        {
            DataContext = new RegionSelectorViewModel();
            InitializeComponent();

            Loaded += RegionSelectorPage_Loaded;

            App.FrostRPC?.SetPage("Region Selector");
        }

        private void RegionSelectorPage_Loaded(object? sender, RoutedEventArgs e)
        {
            var focusSearchCmd = new RoutedCommand();
            CommandBindings.Add(new CommandBinding(focusSearchCmd, (_, __) => FocusSearch()));
            InputBindings.Add(new KeyBinding(focusSearchCmd, Key.E, ModifierKeys.Control));

            var focusRegionCmd = new RoutedCommand();
            CommandBindings.Add(new CommandBinding(focusRegionCmd, (_, __) => FocusRegion()));
            InputBindings.Add(new KeyBinding(focusRegionCmd, Key.K, ModifierKeys.Control));

            AttachBindingsToWindow(focusSearchCmd, focusRegionCmd);

            SearchComboBox.PreviewKeyDown += SearchComboBox_PreviewKeyDown;
            RegionComboBox.PreviewKeyDown += ComboBoxOpenOnArrow_PreviewKeyDown;

            SearchComboBox.Loaded += (_, __) =>
            {
                if (SearchComboBox.Template.FindName("PART_EditableTextBox", SearchComboBox) is TextBox tb)
                {
                    tb.PreviewKeyDown += SearchEditable_PreviewKeyDown;
                }
            };
        }

        private void AttachBindingsToWindow(RoutedCommand focusSearchCmd, RoutedCommand focusRegionCmd)
        {
            if (_windowBindingsAttached)
                return;

            var wnd = Window.GetWindow(this);
            if (wnd == null)
            {
                Dispatcher.BeginInvoke(new System.Action(() => AttachBindingsToWindow(focusSearchCmd, focusRegionCmd)));
                return;
            }

            wnd.CommandBindings.Add(new CommandBinding(focusSearchCmd, (_, __) => FocusSearch()));
            wnd.InputBindings.Add(new KeyBinding(focusSearchCmd, Key.E, ModifierKeys.Control));

            wnd.CommandBindings.Add(new CommandBinding(focusRegionCmd, (_, __) => FocusRegion()));
            wnd.InputBindings.Add(new KeyBinding(focusRegionCmd, Key.K, ModifierKeys.Control));

            _windowBindingsAttached = true;
        }

        private void FocusSearch()
        {
            if (SearchComboBox.Template.FindName("PART_EditableTextBox", SearchComboBox) is TextBox tb)
            {
                tb.Focus();
                tb.Select(tb.Text?.Length ?? 0, 0);
            }
            else
            {
                SearchComboBox.Focus();
            }
        }

        private void FocusRegion()
        {
            RegionComboBox.Focus();
        }

        private void SearchComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                if (!SearchComboBox.IsDropDownOpen)
                {
                    SearchComboBox.IsDropDownOpen = true;
                    if (SearchComboBox.Items.Count > 0)
                    {
                        SearchComboBox.SelectedIndex = e.Key == Key.Down ? 0 : SearchComboBox.Items.Count - 1;
                        Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Action(() =>
                        {
                            if (SearchComboBox.ItemContainerGenerator.ContainerFromIndex(SearchComboBox.SelectedIndex) is ComboBoxItem item)
                                item.Focus();
                        }));
                    }
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                var vm = DataContext as RegionSelectorViewModel;
                if (vm == null)
                {
                    e.Handled = true;
                    return;
                }

                // If dropdown is open with a selected item, accept it first.
                if (SearchComboBox.IsDropDownOpen && SearchComboBox.SelectedItem != null)
                {
                    SearchComboBox.IsDropDownOpen = false;
                    e.Handled = true;
                    return;
                }

                // If we have not performed an initial search yet, invoke the Search button (SearchCommand).
                // If a search has already been performed, invoke Load More (LoadMoreCommand).
                if (!vm.HasSearched)
                {
                    if (vm.SearchCommand?.CanExecute(null) ?? false)
                        vm.SearchCommand.Execute(null);
                }
                else
                {
                    if (vm.LoadMoreCommand?.CanExecute(null) ?? false)
                        vm.LoadMoreCommand.Execute(null);
                }

                e.Handled = true;
            }
        }

        private void SearchEditable_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                if (!SearchComboBox.IsDropDownOpen)
                {
                    SearchComboBox.IsDropDownOpen = true;
                    if (SearchComboBox.Items.Count > 0)
                    {
                        SearchComboBox.SelectedIndex = e.Key == Key.Down ? 0 : SearchComboBox.Items.Count - 1;
                        Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Action(() =>
                        {
                            if (SearchComboBox.ItemContainerGenerator.ContainerFromIndex(SearchComboBox.SelectedIndex) is ComboBoxItem item)
                                item.Focus();
                        }));
                    }
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                var vm = DataContext as RegionSelectorViewModel;
                if (vm == null)
                {
                    e.Handled = true;
                    return;
                }

                // Accept highlighted dropdown item first
                if (SearchComboBox.IsDropDownOpen && SearchComboBox.SelectedItem != null)
                {
                    SearchComboBox.IsDropDownOpen = false;
                    e.Handled = true;
                    return;
                }

                if (!vm.HasSearched)
                {
                    if (vm.SearchCommand?.CanExecute(null) ?? false)
                        vm.SearchCommand.Execute(null);
                }
                else
                {
                    if (vm.LoadMoreCommand?.CanExecute(null) ?? false)
                        vm.LoadMoreCommand.Execute(null);
                }

                e.Handled = true;
            }
        }

        private void ComboBoxOpenOnArrow_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                if (sender is ComboBox cb)
                {
                    if (!cb.IsDropDownOpen)
                    {
                        cb.IsDropDownOpen = true;
                        if (cb.Items.Count > 0)
                        {
                            cb.SelectedIndex = e.Key == Key.Down ? 0 : cb.Items.Count - 1;
                            Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Action(() =>
                            {
                                if (cb.ItemContainerGenerator.ContainerFromIndex(cb.SelectedIndex) is ComboBoxItem item)
                                    item.Focus();
                            }));
                        }
                    }
                }

                e.Handled = true;
            }
        }
    }
}