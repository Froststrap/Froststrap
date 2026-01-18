using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using Froststrap.UI.ViewModels.Installer;
using Froststrap.Resources;

namespace Froststrap.UI.Elements.Installer.Pages
{
	/// <summary>
	/// Interaction logic for WelcomePage.axaml
	/// </summary>
	public partial class WelcomePage : UserControl
	{
		private readonly WelcomeViewModel _viewModel = new();

		public WelcomePage()
		{
			DataContext = _viewModel;
			InitializeComponent();

			if (this.VisualRoot is MainWindow window)
				window.SetButtonEnabled("next", true);
		}

		protected override void OnLoaded(RoutedEventArgs e)
		{
			base.OnLoaded(e);

			if (this.VisualRoot is MainWindow window)
			{
				window.SetNextButtonText(Strings.Common_Navigation_Next);

				window.SetButtonEnabled("back", false);

				window.SetButtonEnabled("next", true);

				window.NextPageCallback = null;
			}
		}
	}
}