using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    internal class GlobalSettingsDialogService : IDialogServiceGlobal
    {
        private readonly MainWindowViewModel _mainVm;

        public GlobalSettingsDialogService(MainWindowViewModel mainVm)
        {
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));
        }

        public async Task OpenGlobalSettingsEditorAsync()
        {
            _mainVm.NavigateToGlobalSettingsEditorCommand.Execute(null);
            await Task.CompletedTask;
        }
    }
    public partial class GlobalSettingsPage : UserControl
    {
        private bool _viewModelSetUp = false;

        public GlobalSettingsPage()
        {
            InitializeComponent();

            App.FrostRPC?.SetPage("Global Settings");
        }

        private void ValidateUInt32(object? sender, TextInputEventArgs e)
        {
            if (sender is TextBox textBox && !string.IsNullOrEmpty(e.Text))
            {
                string currentText = textBox.Text ?? string.Empty;
                int caretIndex = textBox.CaretIndex;
                string newText = currentText.Insert(caretIndex, e.Text);

                e.Handled = !uint.TryParse(newText, out _);
            }
        }

        private void ValidateFloat(object? sender, TextInputEventArgs e)
        {
            if (sender is TextBox textBox && !string.IsNullOrEmpty(e.Text))
            {
                string currentText = textBox.Text ?? string.Empty;
                int caretIndex = textBox.CaretIndex;
                string newText = currentText.Insert(caretIndex, e.Text);

                e.Handled = !Regex.IsMatch(newText, @"^-?\d*\.?\d*$");
            }
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

                App.Logger.WriteLine("GlobalSettingsPage", "ViewModel reinitialized and data reloaded.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("GlobalSettingsPage", ex);
                DataContext = new GlobalSettingsViewModel();
            }
        }
    }
}