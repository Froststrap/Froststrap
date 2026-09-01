using Avalonia.Controls;

namespace Froststrap.UI.Elements.Settings.Pages
{
    internal partial class QuickPlayPage : UserControl
    {
        public QuickPlayPage()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("Quick Play");
        }
    }
}