// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Froststrap.Localisation;
using Froststrap.Models;
using LucideAvalonia.Enum;
using System.Windows.Input;

namespace Froststrap.UI.Elements.Controls
{
    /// <summary>
    /// Shared command surface for the game context menu. Page view models
    /// implement this so GameMenuFlyout can drive their commands.
    /// </summary>
    internal interface IGameMenuActions
    {
        ICommand JoinGameCommand { get; }
        ICommand RejoinLastServerCommand { get; }
        ICommand JoinBestRegionCommand { get; }
        ICommand ViewServersCommand { get; }
        ICommand ViewRobloxServersCommand { get; }
        ICommand ShowPrivateServersCommand { get; }
        ICommand ViewSubplacesCommand { get; }
        ICommand VisitPageCommand { get; }

        bool CanJoinBestRegion { get; }
        bool HasActiveAccount { get; }
    }

    /// <summary>
    /// Reusable context menu for game cards: join/rejoin/best region,
    /// server lists, subplaces and the game page link.
    /// </summary>
    internal class GameMenuFlyout : MenuFlyout
    {
        private readonly MenuItem _join;
        private readonly MenuItem _rejoin;
        private readonly MenuItem _joinBestRegion;
        private readonly MenuItem _trackedServers;
        private readonly MenuItem _robloxServers;
        private readonly MenuItem _privateServers;
        private readonly MenuItem _viewSubplaces;
        private readonly MenuItem _visitPage;

        public GameMenuFlyout()
        {
            Placement = PlacementMode.Pointer;
            _join = CreateItem(LucideIconNames.Play, Strings.Menu_QuickPlay_JoinGame);
            _rejoin = CreateItem(LucideIconNames.RotateCcwClock, Strings.Menu_QuickPlay_RejoinLastServer);
            _joinBestRegion = CreateItem(LucideIconNames.Globe, Strings.Menu_QuickPlay_JoinBestRegion);
            _trackedServers = CreateItem(LucideIconNames.FileCode, Strings.Menu_QuickPlay_TrackedServers);
            _robloxServers = CreateItem(LucideIconNames.Server, Strings.Menu_QuickPlay_RobloxServers);
            _privateServers = CreateItem(LucideIconNames.LockKeyholeOpen, Strings.Menu_QuickPlay_PrivateServers);
            _viewSubplaces = CreateItem(LucideIconNames.Layers, Strings.Menu_QuickPlay_ViewSubplaces);
            _visitPage = CreateItem(LucideIconNames.ExternalLink, Strings.Menu_QuickPlay_VisitPage);

            Items.Add(_join);
            Items.Add(_rejoin);
            Items.Add(_joinBestRegion);
            Items.Add(new Separator());
            Items.Add(_trackedServers);
            Items.Add(_robloxServers);
            Items.Add(_privateServers);
            Items.Add(_viewSubplaces);
            Items.Add(new Separator());
            Items.Add(_visitPage);

            Opening += (_, _) => RefreshItems();
        }

        private static MenuItem CreateItem(LucideIconNames icon, string header)
        {
            return new MenuItem
            {
                Header = header,
                Icon = new LucideAvalonia.Lucide { Icon = icon, Height = 16, Width = 16 }
            };
        }

        private void RefreshItems()
        {
            if (Target?.DataContext is not QuickPlayGameItem item)
                return;

            var actions = FindActions();
            if (actions == null)
                return;

            _join.Command = actions.JoinGameCommand;
            _rejoin.Command = actions.RejoinLastServerCommand;
            _joinBestRegion.Command = actions.JoinBestRegionCommand;
            _trackedServers.Command = actions.ViewServersCommand;
            _robloxServers.Command = actions.ViewRobloxServersCommand;
            _privateServers.Command = actions.ShowPrivateServersCommand;
            _viewSubplaces.Command = actions.ViewSubplacesCommand;
            _visitPage.Command = actions.VisitPageCommand;

            foreach (var menuItem in new[] { _join, _rejoin, _joinBestRegion, _trackedServers, _robloxServers, _privateServers, _viewSubplaces, _visitPage })
                menuItem.CommandParameter = item;

            _joinBestRegion.IsEnabled = actions.CanJoinBestRegion;
            _privateServers.IsVisible = actions.HasActiveAccount;
            _trackedServers.IsVisible = item.Source == GameSource.Tracked;
            _trackedServers.Header = string.Format(
                CultureInfo.InvariantCulture,
                Strings.Menu_QuickPlay_TrackedServers,
                item.ServerCount);
        }

        private IGameMenuActions? FindActions()
        {
            if (Target is not Visual visual)
                return null;

            foreach (var ancestor in visual.GetVisualAncestors())
            {
                if (ancestor is StyledElement { DataContext: IGameMenuActions actions })
                    return actions;
            }

            return null;
        }
    }
}
