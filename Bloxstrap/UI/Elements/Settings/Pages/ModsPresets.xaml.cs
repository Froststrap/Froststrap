using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class ModsPresets
    {
        public ModsPresets()
        {
            DataContext = new ModsPresetsViewModel();
            InitializeComponent();
        }
    }
}