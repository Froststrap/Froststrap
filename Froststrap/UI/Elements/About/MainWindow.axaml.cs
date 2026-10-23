// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel;
using System.Reflection;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Froststrap.UI.Elements.About.Pages;
using Froststrap.UI.ViewModels.About;
using Froststrap.Utility;

namespace Froststrap.UI.Elements.About
{
    internal partial class MainWindow : Base.AvaloniaWindow
    {
        private readonly MainWindowViewModel? _viewModel;

        protected override bool ApplyTopPadding => false;

        public MainWindow()
        {
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;
            Closed += (_, _) => App.AboutOpen = false;

            InitializeComponent();

            Loaded += (_, _) => NavView.IsPaneOpen = false;

            App.FrostRPC?.SetDialog("About");

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            _viewModel.NavigateToAboutCommand.Execute(null);

            UpdateSelectedNavigationViewItem(_viewModel.SelectedPage);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_viewModel == null) return;

            if (e.PropertyName == nameof(MainWindowViewModel.CurrentPage))
            {
                UpdatePageView(_viewModel.CurrentPage);
            }

            if (e.PropertyName == nameof(MainWindowViewModel.SelectedPage))
            {
                UpdateSelectedNavigationViewItem(_viewModel.SelectedPage);
            }
        }

        private void UpdatePageView(object? viewModel)
        {
            var pageControl = this.FindControl<TransitioningContentControl>("PageContentControl");
            if (pageControl == null)
                return;

            if (viewModel == null)
                return;

            var view = ResolveViewForViewModel(viewModel);
            if (view != null)
            {
                view.DataContext = viewModel;
                pageControl.Content = view;
            }
        }

        private static Control? ResolveViewForViewModel(object viewModel) => viewModel switch
        {
            AboutViewModel => new AboutPage(),
            LicensesViewModel => new LicensesPage(),
            _ => null
        };

        private void UpdateSelectedNavigationViewItem(string selectedTag)
        {
            var navView = this.FindControl<FANavigationView>("NavView");
            if (navView == null) return;

            foreach (var item in navView.MenuItems)
            {
                if (item is FANavigationViewItem navItem && navItem.Tag is string tag)
                {
                    if (tag == selectedTag)
                    {
                        navView.SelectedItem = navItem;
                        return;
                    }
                }
            }
        }

        private void NavView_ItemInvoked(object? sender, FANavigationViewItemInvokedEventArgs e)
        {
            if (e.InvokedItemContainer is FANavigationViewItem navItem && navItem.Tag is string tag)
            {
                switch (tag)
                {
                    case "about":
                        _viewModel?.NavigateToAboutCommand.Execute(null);
                        break;

                    case "licenses":
                        _viewModel?.NavigateToLicensesCommand.Execute(null);
                        break;

                    case "onboarding":
                        _viewModel?.RestartOnboardingCommand.Execute(null);
                        break;
                }
            }
        }
    }
}
