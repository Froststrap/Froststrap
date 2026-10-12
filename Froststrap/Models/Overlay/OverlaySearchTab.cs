// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.UI.ViewModels;

namespace Froststrap.Models.Overlay
{
    internal enum OverlaySearchTab
    {
        Games,
        Profiles,
        Items,
        Groups,
        Market,
        Docs,
        DevForum
    }

    internal class OverlaySearchTabItem : NotifyPropertyChangedViewModel
    {
        public OverlaySearchTab Kind { get; init; }

        public string Title { get; init; } = String.Empty;

        public bool IsEnabled { get; init; } = true;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}