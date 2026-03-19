using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    public partial class ShortcutsPage : UserControl
    {
        public ShortcutsPage()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("Shortcut");

            DataContextChanged += (s, e) =>
            {
                if (DataContext is ShortcutsViewModel vm)
                {
                    vm.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == nameof(ShortcutsViewModel.IsSearchFlyoutOpen))
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                var searchBox = this.FindControl<TextBox>("SearchTextBox");
                                if (searchBox == null) return;

                                if (vm.IsSearchFlyoutOpen)
                                    FlyoutBase.ShowAttachedFlyout(searchBox);
                                else
                                    FlyoutBase.GetAttachedFlyout(searchBox)?.Hide();
                            });
                        }
                    };
                }
            };
        }

        private void OnSearchButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout(SearchTextBox);
        }
    }
}