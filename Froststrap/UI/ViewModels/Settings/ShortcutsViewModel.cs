// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using Froststrap.RobloxInterfaces;

namespace Froststrap.UI.ViewModels.Settings
{
    internal class ShortcutsViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        // Use .lnk as the canonical name.
        // Shortcut.cs resolves the correct filename internally
        public ShortcutTask DesktopIconTask { get; } = new("Desktop", Paths.Desktop, $"{App.ProjectName}.lnk");
        public ShortcutTask StartMenuIconTask { get; } = new("StartMenu", Paths.WindowsStartMenu, $"{App.ProjectName}.lnk");
        public ShortcutTask PlayerIconTask { get; } = new("RobloxPlayer", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRoblox}.lnk", LaunchEntry.GetCliArgs(LaunchMode.Player));
        public ShortcutTask StudioIconTask { get; } = new("RobloxStudio", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRobloxStudio}.lnk", LaunchEntry.GetCliArgs(LaunchMode.Studio));
        public ShortcutTask SettingsIconTask { get; } = new("Settings", Paths.Desktop, $"{Strings.Menu_Title}.lnk", "--settings");
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
        private const int SearchPageSize = 10;
        private readonly List<OmniSearchContent> _searchBuffer = [];
        private string? _searchCursor;
        private bool _searchExhausted = true;
        private int _searchGeneration;
        private bool _isLoadingMoreSearch;
        private bool _isProcessingSelection;
        private bool _disposed;
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
        public IRelayCommand ClearSearchCommand { get; }
        public IRelayCommand LoadMoreSearchResultsCommand { get; }
        #endregion

        public ShortcutsViewModel()
        {
            PreviewName = Strings.Menu_Shortcuts_NoGameSelected;
            PreviewId = string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, 0);
            ShortcutStatus = Strings.Menu_Shortcuts_Ready;

            CreateGameShortcutCommand = new AsyncRelayCommand(CreateGameShortcut);
            SearchGamesCommand = new AsyncRelayCommand(SearchGamesAsync);
            ClearSearchCommand = new RelayCommand(ClearSearch);
            LoadMoreSearchResultsCommand = new RelayCommand(LoadMoreSearchResults);
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
                    appPath: Paths.Process,
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
                App.Logger.Error($"Unhandled exception: {ex.Message}");
            }
        }

        private void ClearSearch()
        {
            SearchQuery = string.Empty;
            SearchResults.Clear();
            IsSearchFlyoutOpen = false;
            SelectedSearchResult = null;
            ResetSearchPaging();
        }

        private void ResetSearchPaging()
        {
            _searchGeneration++;
            _searchBuffer.Clear();
            _searchCursor = null;
            _searchExhausted = true;
        }

        private void OnSearchQueryChanged(string value)
        {
            if (_isProcessingSelection) return;

            if (long.TryParse(value, out var id))
            {
                PlaceId = id.ToString(CultureInfo.InvariantCulture);
                _ = FetchInfoForId(id, CancellationToken.None);
            }

            ResetSearchPaging();

            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = new CancellationTokenSource();

            _ = DebouncedSearchTriggerAsync(_searchDebounceCts.Token);
        }

        private async Task DebouncedSearchTriggerAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(600, token);
                if (!token.IsCancellationRequested && !IsGameSearchLoading && !string.IsNullOrWhiteSpace(SearchQuery))
                {
                    if (!long.TryParse(SearchQuery, out _))
                    {
                        await SearchGamesAsync();
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        private void OnSelectedSearchResultChanged(OmniSearchContent? value)
        {
            if (value is null)
            {
                PreviewName = Strings.Menu_Shortcuts_NoGameSelected;
                PreviewId = string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, 0);
                PreviewIcon = null;
                ShortcutStatus = Strings.Menu_Shortcuts_Ready;
                return;
            }

            _isProcessingSelection = true;
            PlaceId = value.RootPlaceId.ToString(CultureInfo.InvariantCulture);
            SearchQuery = PlaceId;
            PreviewName = value.Name!;
            PreviewId = string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, value.RootPlaceId);
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

                await UniverseDetails.FetchBulk(id.ToString(CultureInfo.InvariantCulture));
                var details = UniverseDetails.LoadFromCache(id);

                if (details != null)
                {
                    PreviewName = details.Data.Name;
                    PreviewId = string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, id);
                    PreviewIcon = await LoadBitmapFromUrl(details.Thumbnail.ImageUrl, token);
                }
                else
                {
                    PreviewName = string.Format(CultureInfo.CurrentCulture, Strings.Menu_Shortcuts_Game, id);
                    PreviewId = string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, id);
                    PreviewIcon = null;
                }
            }
            catch (Exception)
            {
                PreviewName = string.Format(CultureInfo.CurrentCulture, Strings.Menu_Shortcuts_Game, id);
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

            int generation = ++_searchGeneration;
            IsGameSearchLoading = true;
            try
            {
                var (results, next) = await Experiences.SuggestPageAsync(SearchQuery);

                if (generation != _searchGeneration)
                    return;

                _searchBuffer.Clear();
                _searchBuffer.AddRange(results);
                _searchCursor = next;
                _searchExhausted = String.IsNullOrEmpty(next);

                List<OmniSearchContent> chunk = _searchBuffer.Take(SearchPageSize).ToList();

                await Experiences.LoadSuggestionIconsAsync(chunk);

                if (generation != _searchGeneration)
                    return;

                Dispatcher.UIThread.Post(() =>
                {
                    if (generation != _searchGeneration)
                        return;

                    SearchResults.Clear();
                    foreach (var res in chunk) SearchResults.Add(res);
                    IsSearchFlyoutOpen = SearchResults.Count > 0 && !string.IsNullOrWhiteSpace(SearchQuery);
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Search error: {ex.Message}");
            }
            finally
            {
                IsGameSearchLoading = false;
            }
        }

        public void LoadMoreSearchResults()
        {
            if (_isLoadingMoreSearch)
                return;

            if (String.IsNullOrWhiteSpace(SearchQuery) || long.TryParse(SearchQuery, out _))
                return;

            if (SearchResults.Count < _searchBuffer.Count || (!_searchExhausted && !String.IsNullOrEmpty(_searchCursor)))
                _ = LoadMoreSearchResultsAsync();
        }

        private async Task LoadMoreSearchResultsAsync()
        {
            _isLoadingMoreSearch = true;
            int generation = _searchGeneration;

            try
            {
                if (SearchResults.Count >= _searchBuffer.Count && !_searchExhausted && !String.IsNullOrEmpty(_searchCursor))
                {
                    var (results, next) = await Experiences.SuggestPageAsync(SearchQuery, _searchCursor);

                    if (generation != _searchGeneration)
                        return;

                    int added = 0;

                    foreach (var item in results)
                    {
                        if (_searchBuffer.Any(x => x.UniverseId == item.UniverseId))
                            continue;

                        _searchBuffer.Add(item);
                        added++;
                    }

                    _searchCursor = next;
                    _searchExhausted = added == 0 || String.IsNullOrEmpty(next);
                }

                if (generation != _searchGeneration)
                    return;

                await AppendNextSearchChunkAsync(generation);
            }
            catch (Exception ex) { App.Logger.Error($"Search load more failed: {ex.Message}"); }
            finally { _isLoadingMoreSearch = false; }
        }

        private async Task AppendNextSearchChunkAsync(int generation)
        {
            if (SearchResults.Count >= _searchBuffer.Count)
                return;

            int next = Math.Min(_searchBuffer.Count, SearchResults.Count + SearchPageSize);
            List<OmniSearchContent> chunk = _searchBuffer.GetRange(SearchResults.Count, next - SearchResults.Count);

            await Experiences.LoadSuggestionIconsAsync(chunk, CancellationToken.None);

            if (generation != _searchGeneration)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                if (generation != _searchGeneration)
                    return;

                foreach (var item in chunk)
                {
                    if (!SearchResults.Contains(item))
                        SearchResults.Add(item);
                }
            }, DispatcherPriority.Background);
        }

        private static async Task<Bitmap?> LoadBitmapFromUrl(string? url, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(url)) return null;

            try
            {
                var response = await App.HttpClient.GetByteArrayAsync(new Uri(url), token);
                using var ms = new MemoryStream(response);
                return new Bitmap(ms);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to load preview bitmap: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _searchDebounceCts?.Cancel();
                _searchDebounceCts?.Dispose();
                _searchDebounceCts = null;
            }

            _disposed = true;
        }
    }
}
