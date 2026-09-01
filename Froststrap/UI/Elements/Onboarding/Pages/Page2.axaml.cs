using Avalonia.Controls;
using Froststrap.UI.ViewModels.Onboarding;

namespace Froststrap.UI.Elements.Onboarding.Pages
{
    internal partial class Page2 : UserControl
    {
        public Page2()
        {
            DataContext = new Page2ViewModel();
            InitializeComponent();
        }
    }
}
