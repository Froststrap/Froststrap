using Avalonia.Controls;
using Froststrap.UI.ViewModels.Onboarding;

namespace Froststrap.UI.Elements.Onboarding.Pages
{
    public partial class Page5 : UserControl
    {
        public Page5()
        {
            DataContext = new Page5ViewModel();
            InitializeComponent();
        }
    }
}