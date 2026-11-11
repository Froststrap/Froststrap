// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.ObjectModel;

namespace Froststrap.UI.ViewModels.About
{
    internal class LicenseItem
    {
        public string Header { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    internal class LicensesViewModel : NotifyPropertyChangedViewModel
    {
        public ObservableCollection<LicenseItem> Licenses { get; } = [];

        public LicensesViewModel()
        {
            LoadLicenseData();
        }

        private void LoadLicenseData()
        {
            Licenses.Add(new LicenseItem
            {
                Header = "Bloxstrap",
                Description = Strings.Menu_About_Licenses_MIT,
                Url = "https://github.com/bloxstraplabs/bloxstrap"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "Fishstrap",
                Description = Strings.Menu_About_Licenses_MIT,
                Url = "https://github.com/Fishstrap/Fishstrap"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "AppleBlox",
                Description = "GPL-3.0",
                Url = "https://github.com/AppleBlox/appleblox"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "Vinegar",
                Description = "GPL-3.0",
                Url = "https://github.com/vinegarhq/vinegar"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "Avalonia",
                Description = Strings.Menu_About_Licenses_MIT,
                Url = "https://github.com/AvaloniaUI/Avalonia"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "AnimatedImage",
                Description = Strings.Menu_About_Licenses_Apache,
                Url = "https://github.com/whistyun/AnimatedImage"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "Avalonia WebView",
                Description = Strings.Menu_About_Licenses_MIT,
                Url = "https://github.com/AvaloniaUI/Avalonia.Controls.WebView"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "Fluent Avalonia",
                Description = Strings.Menu_About_Licenses_MIT,
                Url = "https://github.com/amwx/FluentAvalonia"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "LucideAvalonia",
                Description = Strings.Menu_About_Licenses_MIT,
                Url = "https://github.com/MarwanFr/LucideAvaloniaUI"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "CommunityToolKit MVVM",
                Description = Strings.Menu_About_Licenses_MIT,
                Url = "https://github.com/CommunityToolkit/dotnet"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "DiscordRPC-NativeAOT",
                Description = Strings.Menu_About_Licenses_MIT,
                Url = "https://github.com/samse1337/DiscordRPC-NativeAOT"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "MarkDig",
                Description = "BSD 2-Clause License",
                Url = "https://github.com/xoofx/markdig"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "NLog",
                Description = "BSD 3-Clause License",
                Url = "https://github.com/NLog/NLog"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "objc2",
                Description = "Zlib OR Apache-2.0 OR MIT License",
                Url = "https://github.com/madsmtm/objc2"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "zbus",
                Description = "MIT License",
                Url = "https://github.com/z-galaxy/zbus"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "windows_rs",
                Description = "Apache-2.0 OR MIT License",
                Url = "https://github.com/microsoft/windows-rs"
            });

            Licenses.Add(new LicenseItem
            {
                Header = "Velopack",
                Description = "MIT License",
                Url = "https://github.com/velopack/velopack"
            });
        }
    }
}
