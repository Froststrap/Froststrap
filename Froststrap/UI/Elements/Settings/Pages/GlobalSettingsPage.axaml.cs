// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    internal sealed class GlobalSettingsDialogService(MainWindowViewModel mainVm) : IDialogServiceGlobal
    {
        private readonly MainWindowViewModel _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));

        public Task OpenGlobalSettingsEditorAsync()
        {
            _mainVm.NavigateToGlobalSettingsEditorCommand.Execute(null);
            return Task.CompletedTask;
        }
    }

    internal sealed partial class GlobalSettingsPage : UserControl
    {
        public GlobalSettingsPage()
        {
            InitializeComponent();

            AttachedToVisualTree += (_, _) => { if (!MainWindow.IsStagingPages) App.FrostRPC?.SetPage("Global Settings"); };
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            InitializeFreshViewModel();
        }

        private void InitializeFreshViewModel()
        {
            try
            {
                App.GlobalSettings.Load();

                var topLevel = TopLevel.GetTopLevel(this);

                if (topLevel?.DataContext is MainWindowViewModel mainVm)
                {
                    var dialogService = new GlobalSettingsDialogService(mainVm);
                    DataContext = new GlobalSettingsViewModel(dialogService);
                }
                else
                {
                    DataContext = new GlobalSettingsViewModel();
                }

                App.Logger.Info("ViewModel reinitialized and data reloaded.");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to initialise global settings view model");
                DataContext = new GlobalSettingsViewModel();
            }
        }
    }
}
