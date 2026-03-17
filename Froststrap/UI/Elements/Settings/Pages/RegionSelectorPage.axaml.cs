using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    public partial class RegionSelectorPage : UserControl
    {
        private AutoCompleteBox? _searchBox;
        private ComboBox? _regionBox;
        private bool _windowBindingsAttached = false;

        public RegionSelectorPage()
        {
            DataContext = new RegionSelectorViewModel();
            InitializeComponent();

            this.Loaded += RegionSelectorPage_Loaded;
        }

        private void RegionSelectorPage_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _searchBox = this.FindControl<AutoCompleteBox>("SearchComboBox");
            _regionBox = this.FindControl<ComboBox>("RegionComboBox");

            if (_searchBox != null)
            {
                _searchBox.KeyDown += SearchComboBox_KeyDown;
            }

            if (_regionBox != null)
            {
                _regionBox.KeyDown += ComboBoxOpenOnArrow_KeyDown;
            }

            AttachBindingsToWindow();
        }

        private void AttachBindingsToWindow()
        {
            if (_windowBindingsAttached)
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                Dispatcher.UIThread.InvokeAsync(() => AttachBindingsToWindow(), DispatcherPriority.Background);
                return;
            }

            // Set up keyboard shortcuts
            topLevel.KeyDown += (sender, e) =>
            {
                // Ctrl+E: Focus search
                if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.E)
                {
                    FocusSearch();
                    e.Handled = true;
                }
                // Ctrl+K: Focus region
                else if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.K)
                {
                    FocusRegion();
                    e.Handled = true;
                }
            };

            _windowBindingsAttached = true;
        }

        private void FocusSearch()
        {
            if (_searchBox != null)
            {
                _searchBox.Focus();
            }
        }

        private void FocusRegion()
        {
            if (_regionBox != null)
            {
                _regionBox.Focus();
            }
        }

        private void SearchComboBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                if (_searchBox != null && !_searchBox.IsDropDownOpen)
                {
                    _searchBox.IsDropDownOpen = true;
                    e.Handled = true;
                }
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
                if (_searchBox != null && _searchBox.IsDropDownOpen && _searchBox.SelectedItem != null)
                {
                    _searchBox.IsDropDownOpen = false;
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

        private void ComboBoxOpenOnArrow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                if (sender is ComboBox cb && !cb.IsDropDownOpen)
                {
                    cb.IsDropDownOpen = true;
                    e.Handled = true;
                }
            }
        }
    }
}

