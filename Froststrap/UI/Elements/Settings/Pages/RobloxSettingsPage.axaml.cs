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
            _viewModel = new RobloxSettingsViewModel(App.RemoteData);
            DataContext = _viewModel;

            InitializeComponent();
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