using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Froststrap.UI.Elements.ContextMenu;

public partial class MenuContainer : Window
{
	private readonly Watcher _watcher;

	public MenuContainer( Watcher watcher)
    {
        InitializeComponent();
        _watcher = watcher;
    }
}