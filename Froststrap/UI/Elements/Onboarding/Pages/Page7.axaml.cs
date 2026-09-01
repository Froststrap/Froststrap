using Avalonia.Controls;
using Froststrap.UI.ViewModels.Onboarding;

namespace Froststrap.UI.Elements.Onboarding.Pages
{
    internal partial class Page7 : UserControl
    {
        public Page7()
        {
            DataContext = new Page7ViewModel();
            InitializeComponent();
        }
    }
}