using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Froststrap.UI.ViewModels.Dialogs;
using Froststrap.UI.Elements.Base;
using ICSharpCode.SharpZipLib.Zip;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class AddCustomThemeDialog : Base.AvaloniaWindow
    {
        private const int CreateNewTabId = 0;
        private readonly AddCustomThemeViewModel _viewModel;

        private static readonly FilePickerFileType ZipFileType = new(Strings.FileTypes_ZipArchive)
        {
            Patterns = ["*.zip"]
        };

        public bool Created { get; private set; }
        public string ThemeName { get; private set; } = "";
        public bool OpenEditor { get; private set; }

        public AddCustomThemeDialog()
        {
            InitializeComponent();

            _viewModel = new AddCustomThemeViewModel
            {
                Name = GenerateRandomName()
            };

            DataContext = _viewModel;
        }

        private static string GetThemePath(string name) => Path.Combine(Paths.CustomThemes, name, "Theme.xml");

        private static string GenerateRandomName()
        {
            int count = Directory.GetDirectories(Paths.CustomThemes).Length;
            int i = count + 1;
            string name = string.Format(Strings.CustomTheme_DefaultName, i);

            if (File.Exists(GetThemePath(name)))
                name = string.Format(Strings.CustomTheme_DefaultName, $"{i}-{Random.Shared.Next(1, 100000)}");

            return name;
        }

        private static string GetUniqueName(string name)
        {
            const int maxTries = 100;

            if (!File.Exists(GetThemePath(name)))
                return name;

            for (int i = 1; i <= maxTries; i++)
            {
                string newName = $"{name}_{i}";
                if (!File.Exists(GetThemePath(newName)))
                    return newName;
            }

            return $"{name}_{Random.Shared.Next(maxTries + 1, 1_000_000)}";
        }

        private static async Task CreateCustomTheme(string name, CustomThemeTemplate template)
        {
            string dir = Path.Combine(Paths.CustomThemes, name);

            if (Directory.Exists(dir))
                Directory.Delete(dir, true);

            Directory.CreateDirectory(dir);

            string themeFilePath = Path.Combine(dir, "Theme.xml");
            string templateContent = await template.GetFileContents();

            await File.WriteAllTextAsync(themeFilePath, templateContent);
        }

        private bool ValidateCreateNew()
        {
            const string LOG_IDENT = "AddCustomThemeDialog::ValidateCreateNew";
            _viewModel.NameError = "";

            if (string.IsNullOrEmpty(_viewModel.Name))
            {
                _viewModel.NameError = Strings.CustomTheme_Add_Errors_NameEmpty;
                App.Logger.WriteLine(LOG_IDENT, "Name is empty");
                return false;
            }

            var validationResult = PathValidator.IsFileNameValid(_viewModel.Name);
            if (validationResult != PathValidator.ValidationResult.Ok)
            {
                _viewModel.NameError = validationResult switch
                {
                    PathValidator.ValidationResult.IllegalCharacter => Strings.CustomTheme_Add_Errors_NameIllegalCharacters,
                    PathValidator.ValidationResult.ReservedFileName => Strings.CustomTheme_Add_Errors_NameReserved,
                    _ => Strings.CustomTheme_Add_Errors_Unknown
                };
                return false;
            }

            if (File.Exists(GetThemePath(_viewModel.Name)))
            {
                _viewModel.NameError = Strings.CustomTheme_Add_Errors_NameTaken;
                App.Logger.WriteLine(LOG_IDENT, "Theme name already exists");
                return false;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Validation passed for theme: {_viewModel.Name}");
            return true;
        }

        private static bool ValidateImport(AddCustomThemeViewModel viewModel)
        {
            viewModel.FileError = "";

            if (string.IsNullOrEmpty(viewModel.FilePath) || !viewModel.FilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                viewModel.FileError = Strings.CustomTheme_Add_Errors_FileNotZip;
                return false;
            }

            try
            {
                using var zipFile = System.IO.Compression.ZipFile.OpenRead(viewModel.FilePath);
                bool foundThemeFile = zipFile.Entries.Any(entry =>
                    Path.GetFileName(entry.FullName).Equals("Theme.xml", StringComparison.OrdinalIgnoreCase));

                if (!foundThemeFile)
                {
                    viewModel.FileError = Strings.CustomTheme_Add_Errors_ZipMissingThemeFile;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AddCustomThemeDialog::ValidateImport", ex);
                viewModel.FileError = Strings.CustomTheme_Add_Errors_ZipInvalidData;
                return false;
            }
        }

        private async void OnOkButtonClicked(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedTab == CreateNewTabId)
            {
                if (!ValidateCreateNew()) return;

                await CreateCustomTheme(_viewModel.Name, _viewModel.Template);

                Created = true;
                ThemeName = _viewModel.Name;
                OpenEditor = true;
                Close();
            }
            else
            {
                if (!ValidateImport(_viewModel)) return;
                await Import();
            }
        }

        private async Task Import()
        {
            string fileName = Path.GetFileNameWithoutExtension(_viewModel.FilePath);
            string name = GetUniqueName(fileName);
            string finalDir = Path.Combine(Paths.CustomThemes, name);
            string staging = Path.Combine(Path.GetTempPath(), "theme-import-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(staging);
                Directory.CreateDirectory(finalDir);

                await Task.Run(() =>
                {
                    var fastZip = new FastZip();
                    fastZip.ExtractZip(_viewModel.FilePath, staging, null);

                    var entries = Directory.GetFileSystemEntries(staging);

                    if (entries.Length == 1 && Directory.Exists(entries[0]))
                    {
                        Directory.Delete(finalDir, true);
                        MoveDirectory(entries[0], finalDir);
                    }
                    else
                    {
                        foreach (var entry in entries)
                        {
                            string dest = Path.Combine(finalDir, Path.GetFileName(entry));
                            if (Directory.Exists(entry))
                                MoveDirectory(entry, dest);
                            else
                                File.Copy(entry, dest, true);
                        }
                    }
                });

                Created = true;
                ThemeName = name;
                OpenEditor = false;
                Close();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AddCustomThemeDialog::Import", ex);
                _viewModel.FileError = Strings.CustomTheme_Add_Errors_Unknown;
            }
            finally
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, true);
            }
        }

        private async void OnImportButtonClicked(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Strings.Common_ImportFromFile,
                FileTypeFilter = [ZipFileType],
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                _viewModel.FilePath = files[0].Path.LocalPath;
            }
        }

        private void OnCancelButtonClicked(object sender, RoutedEventArgs e) => Close();

        private static void MoveDirectory(string source, string dest)
        {
            if (AreOnSameVolume(source, dest))
            {
                try
                {
                    Directory.Move(source, dest);
                    return;
                }
                catch (IOException)
                {
                    // Fall through
                }
            }

            CopyDirectory(source, dest);
            Directory.Delete(source, true);
        }

        private static bool AreOnSameVolume(string path1, string path2)
        {
            return string.Equals(
                Path.GetPathRoot(path1),
                Path.GetPathRoot(path2),
                StringComparison.OrdinalIgnoreCase);
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }
    }
}
