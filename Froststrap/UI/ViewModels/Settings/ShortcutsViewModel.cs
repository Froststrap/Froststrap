/*
*  Froststrap
*  Copyright (c) Froststrap Team
*
*  This file is part of Froststrap and is distributed under the terms of the
*  GNU Affero General Public License, version 3 or later.
*
*  SPDX-License-Identifier: AGPL-3.0-or-later
*/

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using Avalonia.Media.Imaging;

namespace Froststrap.UI.ViewModels.Settings
{
    public class ShortcutsViewModel : NotifyPropertyChangedViewModel
    {
        private readonly string LOG_IDENT = "ShortcutsViewModel";

        // Use .lnk as the canonical name.
        // Shortcut.cs resolves the correct filename internally
        public ShortcutTask DesktopIconTask { get; } = new("Desktop", Paths.Desktop, $"{App.ProjectName}.lnk");
        public ShortcutTask StartMenuIconTask { get; } = new("StartMenu", Paths.WindowsStartMenu, $"{App.ProjectName}.lnk");
        public ShortcutTask PlayerIconTask { get; } = new("RobloxPlayer", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRoblox}.lnk", "-player");
        public ShortcutTask StudioIconTask { get; } = new("RobloxStudio", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRobloxStudio}.lnk", "-studio");
        public ShortcutTask SettingsIconTask { get; } = new("Settings", Paths.Desktop, $"{Strings.Menu_Title}.lnk", "-settings");
        public ExtractIconsTask ExtractIconsTask { get; } = new();

        #region Fields
        private string _searchQuery = "";
        private bool _isGameSearchLoading;
        private string _placeId = "";
        private string _jobId = "";
        private string _accessCode = "";
        private string _previewName = "";
        private string _previewId = "";
        private Bitmap? _previewIcon;
        private string _shortcutStatus = "";
        private bool _isSearchFlyoutOpen;
        private OmniSearchContent? _selectedSearchResult;
        private CancellationTokenSource? _searchDebounceCts;
        private bool _isProcessingSelection = false;
        #endregion

        #region Properties
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                    OnSearchQueryChanged(value);
            }
        }

        public bool IsGameSearchLoading
        {
            get => _isGameSearchLoading;
            set => SetProperty(ref _isGameSearchLoading, value);
        }

        public string PlaceId
        {
            get => _placeId;
            set => SetProperty(ref _placeId, value);
        }

        public string JobId
        {
            get => _jobId;
            set => SetProperty(ref _jobId, value);
        }

        public string AccessCode
        {
            get => _accessCode;
            set => SetProperty(ref _accessCode, value);
        }

        public string PreviewName
        {
            get => _previewName;
            set => SetProperty(ref _previewName, value);
        }

        public string PreviewId
        {
            get => _previewId;
            set => SetProperty(ref _previewId, value);
        }

        public Bitmap? PreviewIcon
        {
            get => _previewIcon;
            set => SetProperty(ref _previewIcon, value);
        }

        public string ShortcutStatus
        {
            get => _shortcutStatus;
            set => SetProperty(ref _shortcutStatus, value);
        }

        public bool IsSearchFlyoutOpen
        {
            get => _isSearchFlyoutOpen;
            set => SetProperty(ref _isSearchFlyoutOpen, value);
        }

        public OmniSearchContent? SelectedSearchResult
        {
            get => _selectedSearchResult;
            set
            {
                if (SetProperty(ref _selectedSearchResult, value))
                    OnSelectedSearchResultChanged(value);
            }
        }

        public ObservableCollection<OmniSearchContent> SearchResults { get; } = [];

        public IAsyncRelayCommand CreateGameShortcutCommand { get; }
        public IAsyncRelayCommand SearchGamesCommand { get; }
        #endregion

        public ShortcutsViewModel()
        {
            PreviewName = Strings.Menu_Shortcuts_NoGameSelected;
            PreviewId = string.Format(Strings.Menu_RegionSelector_ID, 0);
            ShortcutStatus = Strings.Menu_Shortcuts_Ready;

            CreateGameShortcutCommand = new AsyncRelayCommand(CreateGameShortcut);
            SearchGamesCommand = new AsyncRelayCommand(SearchGamesAsync);
        }

        private async Task CreateGameShortcut()
        {
            if (string.IsNullOrEmpty(PlaceId) || PreviewName == Strings.Menu_Shortcuts_NoGameSelected)
            {
                ShortcutStatus = Strings.Menu_Shortcuts_SelectGameFirst;
                return;
            }

            try
            {
                ShortcutStatus = Strings.Menu_Shortcuts_Processing;

                await Shortcut.CreateGameShortcut(
                    appPath: Paths.Application,
                    displayName: PreviewName,
                    placeId: PlaceId,
                    jobId: JobId,
                    accessCode: AccessCode,
                    icon: PreviewIcon,
                    onStatus: status => ShortcutStatus = status
                );

                ShortcutStatus = Strings.Menu_Shortcuts_ShortcutCreated;
            }
            catch (Exception ex)
            {
                ShortcutStatus = Strings.Menu_Shortcuts_ErrorCreatingShortcut;
                App.Logger.WriteLine(LOG_IDENT, $"Error: {ex.Message}");
            }
        }

        private void OnSearchQueryChanged(string value)
        {
            if (_isProcessingSelection) return;

            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = new CancellationTokenSource();

            _ = HandleQueryInputAsync(value, _searchDebounceCts.Token);
        }

        private async Task HandleQueryInputAsync(string value, CancellationToken token)
        {
            try
            {
                await Task.Delay(600, token);

                if (long.TryParse(value, out long id))
                {
                    PlaceId = id.ToString();
                    await FetchInfoForId(id, token);
                }
                else if (!string.IsNullOrWhiteSpace(value))
                {
                    await SearchGamesAsync();
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task<Bitmap?> LoadBitmapFromUrl(string? url, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(url)) return null;

            try
            {
                var response = await App.HttpClient.GetByteArrayAsync(url, token);
                using var ms = new MemoryStream(response);
                return new Bitmap(ms);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to load preview bitmap: {ex.Message}");
                return null;
            }
        }

        private void OnSelectedSearchResultChanged(OmniSearchContent? value)
        {
            if (value is null) return;

            _isProcessingSelection = true;

            PlaceId = value.RootPlaceId.ToString();
            SearchQuery = PlaceId;
            PreviewName = value.Name!;
            PreviewId = string.Format(Strings.Menu_RegionSelector_ID, value.RootPlaceId);
            PreviewIcon = value.ThumbnailBitmap;
            ShortcutStatus = Strings.Menu_Shortcuts_ReadyToCreate;

            _isProcessingSelection = false;
            IsSearchFlyoutOpen = false;
        }

        private async Task FetchInfoForId(long id, CancellationToken token)
        {
            try
            {
                ShortcutStatus = Strings.Menu_Shortcuts_UpdatingPreview;

                await UniverseDetails.FetchBulk(id.ToString());
                var details = UniverseDetails.LoadFromCache(id);

                if (details != null)
                {
                    PreviewName = details.Data.Name;
                    PreviewId = string.Format(Strings.Menu_RegionSelector_ID, id);
                    PreviewIcon = await LoadBitmapFromUrl(details.Thumbnail.ImageUrl, token);
                }
                else
                {
                    PreviewName = string.Format(Strings.Menu_Shortcuts_Game, id);
                    PreviewId = string.Format(Strings.Menu_RegionSelector_ID, id);
                    PreviewIcon = null;
                }
            }
            catch (Exception)
            {
                PreviewName = string.Format(Strings.Menu_Shortcuts_Game, id);
                ShortcutStatus = Strings.Menu_Shortcuts_ReadyWithManualId;
            }
        }

        private async Task SearchGamesAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery) || SearchQuery.Length < 3)
            {
                IsSearchFlyoutOpen = false;
                return;
            }

            IsGameSearchLoading = true;
            try
            {
                var results = await GameSearching.GetGameSearchResultsAsync(SearchQuery);

                if (results != null && results.Count > 0)
                {
                    var thumbRequests = results.Select(x => new ThumbnailRequest
                    {
                        TargetId = (ulong)x.RootPlaceId,
                        Type = ThumbnailType.PlaceIcon,
                        Size = "128x128",
                        Format = ThumbnailFormat.Png,
                        IsCircular = false
                    }).ToList();

                    var fetchedUrls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, CancellationToken.None);

                    for (int i = 0; i < results.Count; i++)
                    {
                        if (fetchedUrls != null && i < fetchedUrls.Length && !string.IsNullOrEmpty(fetchedUrls[i]))
                        {
                            try
                            {
                                var response = await App.HttpClient.GetByteArrayAsync(fetchedUrls[i]);
                                using var ms = new MemoryStream(response);
                                results[i].ThumbnailBitmap = new Bitmap(ms);
                            }
                            catch { }
                        }
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    SearchResults.Clear();
                    if (results != null)
                    {
                        foreach (var res in results) SearchResults.Add(res);
                    }
                    IsSearchFlyoutOpen = SearchResults.Count > 0 && !string.IsNullOrWhiteSpace(SearchQuery);
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Search error: {ex.Message}");
            }
            finally
            {
                IsGameSearchLoading = false;
            }
        }
    }
}