using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using Froststrap.UI.ViewModels.Installer;
using Froststrap.Resources;

namespace Froststrap.UI.Elements.Installer.Pages
{
	/// <summary>
	/// Interaction logic for CompletionPage.axaml
	/// </summary>
	public partial class CompletionPage : UserControl
	{
		private readonly CompletionViewModel _viewModel = new();

		public CompletionPage()
		{
			DataContext = _viewModel;
			InitializeComponent();

			_viewModel.CloseWindowRequest += (_, closeAction) =>
			{
				if (this.VisualRoot is MainWindow window)
				{
					window.CloseAction = closeAction;
					window.Close();
				}
			};
		}

		protected override void OnLoaded(RoutedEventArgs e)
		{
			base.OnLoaded(e);

			if (this.VisualRoot is MainWindow window)
			{
				window.SetNextButtonText(Strings.Common_Navigation_Next);
				window.SetButtonEnabled("back", false);
				window.SetButtonEnabled("next", true);
			}
		}
	}
}