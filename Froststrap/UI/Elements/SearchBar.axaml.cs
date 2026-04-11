using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Froststrap.UI.Elements;

public partial class SearchBar : UserControl
{
    public SearchBar()
    {
        AvaloniaXamlLoader.Load(this);
    }
}