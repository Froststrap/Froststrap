using Avalonia.Controls;
using Froststrap.UI.ViewModels.Onboarding;

namespace Froststrap.UI.Elements.Onboarding.Pages
{
    public partial class Page4 : UserControl
    {
        public Page4()
        {
            DataContext = new Page4ViewModel();
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