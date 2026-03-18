using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    public partial class RegionSelectorPage : UserControl
    {
        private bool _windowBindingsAttached = false;

        public RegionSelectorPage()
        {
            InitializeComponent();

            this.Loaded += RegionSelectorPage_Loaded;

            DataContextChanged += (s, e) =>
            {
                if (DataContext is RegionSelectorViewModel vm)
                {
                    vm.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == nameof(RegionSelectorViewModel.IsSearchFlyoutOpen))
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                if (vm.IsSearchFlyoutOpen)
                                    FlyoutBase.ShowAttachedFlyout(SearchTextBox);
                                else
                                    FlyoutBase.GetAttachedFlyout(SearchTextBox)?.Hide();
                            });
                        }
                    };
                }
            };
        }

        private void RegionSelectorPage_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            SearchTextBox.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Enter)
                {
                    if (DataContext is RegionSelectorViewModel vm)
                    {
                        vm.IsSearchFlyoutOpen = false;
                        if (vm.SearchCommand.CanExecute(null))
                            vm.SearchCommand.Execute(null);
                    }
                    args.Handled = true;
                }
            };

            AttachBindingsToWindow();
        }

        private void AttachBindingsToWindow()
        {
            if (_windowBindingsAttached) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                Dispatcher.UIThread.InvokeAsync(() => AttachBindingsToWindow(), DispatcherPriority.Background);
                return;
            }

            topLevel.KeyDown += (sender, e) =>
            {
                if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.E)
                {
                    SearchTextBox.Focus();
                    e.Handled = true;
                }
            };

            _windowBindingsAttached = true;
        }

        private void OnSearchButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout(SearchTextBox);
        }
    }
}

