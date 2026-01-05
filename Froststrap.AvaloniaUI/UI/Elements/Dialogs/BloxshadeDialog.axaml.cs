using Avalonia.Interactivity;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class BloxshadeDialog : Base.AvaloniaWindow
    {
        public NextAction CloseAction = NextAction.Terminate;

        public BloxshadeDialog()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("Bloxshade");
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}