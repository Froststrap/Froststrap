using Avalonia;
using Avalonia.Controls;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.Elements.Controls
{
    public class BreadcrumbItem
    {
        public string Label { get; set; } = string.Empty;
        public string? PageId { get; set; }
        public bool IsClickable { get; set; }
        public bool IsLast { get; set; }
        public ICommand? NavigateCommand { get; set; }
    }

    public partial class Breadcrumb : UserControl
    {
        public static readonly DirectProperty<Breadcrumb, ObservableCollection<BreadcrumbItem>> ItemsProperty =
            AvaloniaProperty.RegisterDirect<Breadcrumb, ObservableCollection<BreadcrumbItem>>(
                nameof(Items),
                o => o.Items,
                (o, v) => o.Items = v);

        private ObservableCollection<BreadcrumbItem> _items = new();

        public ObservableCollection<BreadcrumbItem> Items
        {
            get => _items;
            set => SetAndRaise(ItemsProperty, ref _items, value);
        }

        public Breadcrumb()
        {
            DataContext = this;
        }
    }
}
