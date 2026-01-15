using System;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Froststrap.Models;
using Froststrap.UI.ViewModels.Settings;
using Froststrap;
using Froststrap.UI.Elements.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
	public partial class RobloxSettingsPage : UserControl
	{
		private RobloxSettingsViewModel? _viewModel;

		public RobloxSettingsPage()
		{
			InitializeComponent();
			this.AttachedToVisualTree += RobloxSettingsPage_AttachedToVisualTree;
		}

		private async void RobloxSettingsPage_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
		{
			App.FrostRPC?.SetPage("Roblox Settings");
			var mainWindow = TopLevel.GetTopLevel(this) as MainWindow;
			mainWindow?.ShowLoading("Loading Roblox Settings...");

			try
			{
				await App.RemoteData.WaitUntilDataFetched();

				_viewModel = new RobloxSettingsViewModel(App.RemoteData);
				DataContext = _viewModel;
			}
			catch (Exception ex)
			{
				App.Logger.WriteLine("RobloxSettingsPage", $"Error while loading remote data: {ex}");

				Frontend.ShowMessageBox(
					$"Failed to load Roblox settings:\n\n{ex.Message}",
					MessageBoxImage.Error
				);
			}
			finally
			{
				mainWindow?.HideLoading();
			}
		}

		private void ValidateUInt32(object? sender, TextInputEventArgs e)
		{
			if (string.IsNullOrEmpty(e.Text)) return;
			e.Handled = !uint.TryParse(e.Text, out _);
		}

		private void ValidateFloat(object? sender, TextInputEventArgs e)
		{
			if (string.IsNullOrEmpty(e.Text)) return;
			e.Handled = !Regex.IsMatch(e.Text, @"^[0-9.]+$");
		}
	}
}