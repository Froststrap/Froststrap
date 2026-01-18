using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using Froststrap.UI.ViewModels.Installer;
using Froststrap.Resources;

namespace Froststrap.UI.Elements.Installer.Pages
{
	/// <summary>
	/// Interaction logic for InstallPage.axaml
	/// </summary>
	public partial class InstallPage : UserControl
	{
		private readonly InstallViewModel _viewModel = new();

		public InstallPage()
		{
			DataContext = _viewModel;
			InitializeComponent();

			_viewModel.SetCanContinueEvent += (_, state) =>
			{
				if (this.VisualRoot is MainWindow window)
					window.SetButtonEnabled("next", state);
			};
		}

		protected override void OnLoaded(RoutedEventArgs e)
		{
			base.OnLoaded(e);

			if (this.VisualRoot is MainWindow window)
			{
				window.SetNextButtonText(Strings.Common_Navigation_Install);

				window.SetButtonEnabled("back", true);

				window.NextPageCallback = NextPageCallback;
			}
		}

		public bool NextPageCallback() => _viewModel.DoInstall();
	}
}