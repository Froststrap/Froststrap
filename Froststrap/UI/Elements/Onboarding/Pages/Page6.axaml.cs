using Avalonia.Controls;
using Froststrap.UI.ViewModels.Onboarding;

namespace Froststrap.UI.Elements.Onboarding.Pages
{
    internal partial class Page6 : UserControl
    {
        public Page6()
        {
            DataContext = new Page6ViewModel();
            InitializeComponent();
        }
    }
}
