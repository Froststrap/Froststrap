using Bloxstrap.UI.ViewModels.AccountManagers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Bloxstrap.UI.Elements.AccountManagers.Pages
{
    /// <summary>
    /// Interaction logic for GamesPage.xaml
    /// </summary>
    public partial class GamesPage
    {
        private GamesViewModel? _viewModel;

        public GamesPage()
        {
            _viewModel = new GamesViewModel();
            DataContext = _viewModel;
            InitializeComponent();
        }

        private void HorizontalScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer && !e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                var parent = VisualTreeHelper.GetParent(scrollViewer) as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }
    }
}