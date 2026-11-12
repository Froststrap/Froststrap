// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Resources;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Diagnostics
{
    internal partial class MainWindowViewModel : NotifyPropertyChangedViewModel
    {
        public bool IsNotLinux { get; } = !OperatingSystem.IsLinux();
        public int TitlebarSpaceHeight { get; } = OperatingSystem.IsMacOS() ? 52 : 40;

        public ICommand OpenFroststrapLogsCommand { get; } = new RelayCommand(() => OpenFolder(Paths.Logs));
        public ICommand OpenRobloxLogsCommand { get; } = new RelayCommand(() => OpenFolder(Paths.RobloxLogs));
        public ICommand TakeScreenshotCommand => new AsyncRelayCommand<Control>(TakeScreenshotAsync);

        private string _screenshotStatus = string.Empty;
        public string ScreenshotStatus
        {
            get => _screenshotStatus;
            set
            {
                SetProperty(ref _screenshotStatus, value);
                OnPropertyChanged(nameof(HasScreenshotStatus));
            }
        }

        public bool HasScreenshotStatus => !string.IsNullOrEmpty(ScreenshotStatus);

        private static void OpenFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            Directory.CreateDirectory(path);
            Froststrap.Utility.Threading.ShellExecute(path);
        }

        private async Task TakeScreenshotAsync(Control? sender)
        {
            var settingsWindow = Elements.Settings.MainWindow.Instance;

            if (!App.SettingsOpen || settingsWindow?.Content is not Control content || content.Bounds.Width < 1 || content.Bounds.Height < 1)
            {
                ScreenshotStatus = Strings.Menu_Diagnostics_Screenshot_NotOpen;
                return;
            }

            var topLevel = sender is null ? null : TopLevel.GetTopLevel(sender);
            if (topLevel is null)
                return;

            try
            {
                double scale = settingsWindow.RenderScaling;
                var pixelSize = new Avalonia.PixelSize((int)Math.Ceiling(content.Bounds.Width * scale), (int)Math.Ceiling(content.Bounds.Height * scale));
                using var bitmap = new RenderTargetBitmap(pixelSize, new Avalonia.Vector(96 * scale, 96 * scale));
                bitmap.Render(content);

                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = Strings.Menu_Diagnostics_Screenshot_Header,
                    SuggestedFileName = $"Froststrap-{DateTime.Now:yyyyMMdd-HHmmss}.png",
                    FileTypeChoices = [FilePickerFileTypes.ImagePng]
                });

                if (file is null)
                    return;

                await using var stream = await file.OpenWriteAsync();
                bitmap.Save(stream, PngBitmapEncoderOptions.Default);

                ScreenshotStatus = Strings.Menu_Diagnostics_Screenshot_Saved;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to take screenshot:");
                ScreenshotStatus = Strings.Menu_Diagnostics_Screenshot_Failed;
            }
        }
    }
}
