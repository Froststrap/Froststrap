// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Animation;
using Froststrap.UI.ViewModels.About;

namespace Froststrap.UI.Elements.About.Pages
{
    internal partial class AboutPage : UserControl
    {
        public AboutPage()
        {
            DataContext = new AboutViewModel();
            InitializeComponent();
        }
    }
}
