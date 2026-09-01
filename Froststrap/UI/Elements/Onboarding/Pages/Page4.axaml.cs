using Avalonia.Controls;
using Froststrap.UI.ViewModels.Onboarding;

namespace Froststrap.UI.Elements.Onboarding.Pages
{
    internal partial class Page4 : UserControl
    {
        public Page4()
        {
            DataContext = new Page4ViewModel();
            InitializeComponent();
        }
    }
}