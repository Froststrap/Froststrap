using Avalonia.Controls;
using Froststrap.UI.ViewModels.Onboarding;

namespace Froststrap.UI.Elements.Onboarding.Pages
{
    internal partial class Page3 : UserControl
    {
        public Page3()
        {
            DataContext = new Page3ViewModel();
            InitializeComponent();
        }
    }
}
