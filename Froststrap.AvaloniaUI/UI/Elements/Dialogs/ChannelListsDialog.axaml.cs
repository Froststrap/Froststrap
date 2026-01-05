using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Froststrap.UI.ViewModels.Dialogs;
using System;
using System.Linq;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class ChannelListsDialog : Base.AvaloniaWindow
    {
        public ChannelListsDialog()
        {
            InitializeComponent();
            DataContext = new ChannelListsViewModel();
        }

        private async void ChannelDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control)
            {
                if (sender is DataGrid dataGrid)
                {
                    var selectedItems = dataGrid.SelectedItems
                        .OfType<DeployInfoDisplay>()
                        .Select(i => i.ChannelName)
                        .ToList();

                    if (selectedItems.Count > 0)
                    {
                        var textToCopy = string.Join(Environment.NewLine, selectedItems);

                        var topLevel = TopLevel.GetTopLevel(this);
                        if (topLevel?.Clipboard != null)
                        {
                            await topLevel.Clipboard.SetTextAsync(textToCopy);
                        }

                        e.Handled = true;
                    }
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}