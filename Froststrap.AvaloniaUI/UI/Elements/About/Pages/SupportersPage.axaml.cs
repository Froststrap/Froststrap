using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Froststrap.UI.ViewModels.About;

namespace Froststrap.UI.Elements.About.Pages;

public partial class SupportersPage : UserControl
{
	private readonly SupportersViewModel _viewModel = new();

	public SupportersPage()
    {
		DataContext = _viewModel;
		InitializeComponent();
    }
}