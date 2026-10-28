// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using CommunityToolkit.Mvvm.Input;

namespace Froststrap.UI.ViewModels.Diagnostics
{
    internal partial class MainWindowViewModel : NotifyPropertyChangedViewModel
    {
        public bool IsNotLinux { get; } = !OperatingSystem.IsLinux();
        public int TitlebarSpaceHeight { get; } = OperatingSystem.IsMacOS() ? 52 : 40;

        private string _exampleText = string.Empty;
        public string ExampleText
        {
            get => _exampleText;
            set => SetProperty(ref _exampleText, value);
        }
    }
}
