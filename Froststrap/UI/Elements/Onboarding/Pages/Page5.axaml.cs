using Avalonia.Controls;
using Froststrap.UI.ViewModels.Onboarding;

namespace Froststrap.UI.Elements.Onboarding.Pages
{
    internal partial class Page5 : UserControl
    {
        public Page5()
        {
            DataContext = new Page5ViewModel();
            InitializeComponent();

            Loaded += (_, _) =>
            {
                ShortcutsGrid.ColumnDefinitions[1].Width = OperatingSystem.IsWindows()
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(0);
            };
        }
    }
}