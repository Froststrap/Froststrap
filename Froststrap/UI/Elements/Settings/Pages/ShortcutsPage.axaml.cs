using Avalonia.Controls;

namespace Froststrap.UI.Elements.Settings.Pages
{
    internal partial class ShortcutsPage : UserControl
    {
        public ShortcutsPage()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("Shortcut");

            Loaded += (_, _) =>
            {
                ShortcutsGrid.ColumnDefinitions[1].Width = OperatingSystem.IsWindows()
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(0);
            };
        }
    }
}