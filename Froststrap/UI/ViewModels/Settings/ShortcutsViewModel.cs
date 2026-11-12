// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using Avalonia.Media.Imaging;

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
        private bool _isProcessingSelection;
        private int _linkResolveSeq;
        private string _gameLink = "";
        private string _linkPlaceId = "";
        private string _linkJobId = "";
        private string _linkAccessCode = "";
        private string _linkPreviewName = "";
        private string _linkPreviewId = "";
        private Bitmap? _linkPreviewIcon;
        private string _linkShortcutStatus = "";
        private CancellationTokenSource? _gameLinkDebounceCts;
        private int _gameLinkResolveSeq;
        private bool _linkAccessCodeIsLinkCode;
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

        public string GameLink
        {
            get => _gameLink;
            set
            {
                if (SetProperty(ref _gameLink, value))
                    OnGameLinkChanged(value);
            }
        }

        public string LinkPreviewName
        {
            get => _linkPreviewName;
            set => SetProperty(ref _linkPreviewName, value);
        }

        public string LinkPreviewId
        {
            get => _linkPreviewId;
            set => SetProperty(ref _linkPreviewId, value);
        }

        public Bitmap? LinkPreviewIcon
        {
            get => _linkPreviewIcon;
            set => SetProperty(ref _linkPreviewIcon, value);
        }

        public string LinkShortcutStatus
        {
            get => _linkShortcutStatus;
            set => SetProperty(ref _linkShortcutStatus, value);
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
        public IAsyncRelayCommand CreateLinkShortcutCommand { get; }
        public IAsyncRelayCommand SearchGamesCommand { get; }
        public IRelayCommand ClearSearchCommand { get; }
        #endregion

        public ShortcutsViewModel()
        {
            PreviewName = Strings.Menu_Shortcuts_NoGameSelected;
            PreviewId = string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, 0);
            ShortcutStatus = Strings.Menu_Shortcuts_Ready;

            LinkPreviewName = Strings.Menu_Shortcuts_NoGameSelected;
            LinkPreviewId = string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, 0);
            LinkShortcutStatus = Strings.Menu_Shortcuts_Ready;

            CreateGameShortcutCommand = new AsyncRelayCommand(CreateGameShortcut);
            CreateLinkShortcutCommand = new AsyncRelayCommand(CreateLinkShortcut);
            SearchGamesCommand = new AsyncRelayCommand(SearchGamesAsync);
            ClearSearchCommand = new RelayCommand(ClearSearch);
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
                    linkCode: null,
                    icon: PreviewIcon,
                    onStatus: status => ShortcutStatus = status
                );

                ShortcutStatus = string.Format(CultureInfo.CurrentCulture, Strings.Menu_Shortcuts_Created, PreviewName);
            }
            catch (Exception ex)
            {
                ShortcutStatus = Strings.Menu_Shortcuts_Failed;
                App.Logger.Error(ex, "Failed to create game shortcut");
            }
        }

        private async Task CreateLinkShortcut()
        {
            if (string.IsNullOrEmpty(_linkPlaceId) || LinkPreviewName == Strings.Menu_Shortcuts_NoGameSelected)
            {
                LinkShortcutStatus = Strings.Menu_Shortcuts_SelectGameFirst;
                return;
            }

            try
            {
                LinkShortcutStatus = Strings.Menu_Shortcuts_Processing;

                await Shortcut.CreateGameShortcut(
                    appPath: Paths.Process,
                    displayName: LinkPreviewName,
                    placeId: _linkPlaceId,
                    jobId: _linkJobId,
                    accessCode: _linkAccessCodeIsLinkCode ? null : _linkAccessCode,
                    linkCode: _linkAccessCodeIsLinkCode ? _linkAccessCode : null,
                    icon: LinkPreviewIcon,
                    onStatus: status => LinkShortcutStatus = status
                );

                LinkShortcutStatus = string.Format(CultureInfo.CurrentCulture, Strings.Menu_Shortcuts_Created, LinkPreviewName);
            }
            catch (Exception ex)
            {
                LinkShortcutStatus = Strings.Menu_Shortcuts_Failed;
                App.Logger.Error(ex, "Failed to create link shortcut");
            }
        }

        private void ClearSearch()
        {
            _isProcessingSelection = true;
            _linkResolveSeq++;
            _searchDebounceCts?.Cancel();

            SearchQuery = string.Empty;
            SearchResults.Clear();
            IsSearchFlyoutOpen = false;
            SelectedSearchResult = null;
            ResetSelectedGame();
            _isProcessingSelection = false;
        }

        private void ResetSelectedGame()
        {
            PlaceId = string.Empty;
            JobId = string.Empty;
            AccessCode = string.Empty;
            PreviewName = Strings.Menu_Shortcuts_NoGameSelected;
            PreviewId = string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, 0);
            PreviewIcon = null;
            ShortcutStatus = Strings.Menu_Shortcuts_Ready;
        }

        private void OnSearchQueryChanged(string value)
        {
            if (_isProcessingSelection) return;

            if (string.IsNullOrWhiteSpace(value))
            {
                ClearSearch();
                return;
            }

            if (long.TryParse(value, out var id) && id > 0)
            {
                _linkResolveSeq++;
                PlaceId = id.ToString(CultureInfo.InvariantCulture);
                SetPendingGamePreview(id);
                _ = FetchInfoForId(id, CancellationToken.None);
            }
            else if (LooksLikeRobloxLink(value))
            {
                _linkResolveSeq++;
                _searchDebounceCts?.Cancel();
                SearchResults.Clear();
                IsSearchFlyoutOpen = false;
                SelectedSearchResult = null;
                ResetSelectedGame();
                _ = ResolvePastedLinkAsync(value.Trim(), _linkResolveSeq);
                return;
            }

            SelectedSearchResult = null;
            ResetSelectedGame();

            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = new CancellationTokenSource();

            _ = DebouncedSearchTriggerAsync(_searchDebounceCts.Token);
        }

        private static bool LooksLikeRobloxLink(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();

            return trimmed.StartsWith("roblox://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("roblox.com", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("ro.blox.com/", StringComparison.OrdinalIgnoreCase);
        }

        private async Task ResolvePastedLinkAsync(string link, int sequence)
        {
            ShortcutStatus = Strings.Menu_Shortcuts_UpdatingPreview;

            string? command;

            try
            {
                command = await GameJoin.GetLaunchCommandByLink(link);
            }
            catch (Exception ex)
            {
                if (sequence != _linkResolveSeq)
                    return;

                App.Logger.Error($"Failed to resolve pasted game link: {ex.Message}");
                ShortcutStatus = Strings.Menu_Shortcuts_InvalidLink;
                return;
            }

            if (sequence != _linkResolveSeq)
                return;

            if (string.IsNullOrEmpty(command))
            {
                ShortcutStatus = Strings.Menu_Shortcuts_InvalidLink;
                return;
            }

            GameJoinData joinData = GameJoin.GetJoinDataByLaunchCommand(command);

            if (joinData.PlaceId is not long placeId || placeId <= 0)
            {
                ShortcutStatus = Strings.Menu_Shortcuts_InvalidLink;
                return;
            }

            _isProcessingSelection = true;
            PlaceId = placeId.ToString(CultureInfo.InvariantCulture);
            SearchQuery = PlaceId;
            JobId = IsValidJobId(joinData.JobId) ? joinData.JobId! : string.Empty;
            AccessCode = joinData.AccessCode ?? string.Empty;
            SetPendingGamePreview(placeId);
            _isProcessingSelection = false;

            await FetchInfoForId(placeId, CancellationToken.None);
        }

        private void SetPendingGamePreview(long placeId)
        {
            PreviewName = string.Format(CultureInfo.CurrentCulture, Strings.Menu_Shortcuts_Game, placeId);
            PreviewId = string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, placeId);
            PreviewIcon = null;
            ShortcutStatus = Strings.Menu_Shortcuts_UpdatingPreview;
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
            _linkResolveSeq++;
            _searchDebounceCts?.Cancel();

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

        private void OnGameLinkChanged(string value)
        {
            _gameLinkResolveSeq++;
            _gameLinkDebounceCts?.Cancel();
            _gameLinkDebounceCts?.Dispose();
            _gameLinkDebounceCts = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                ResetLinkSelection();
                return;
            }

            ResetLinkSelection();
            LinkShortcutStatus = Strings.Menu_Shortcuts_UpdatingPreview;

            _gameLinkDebounceCts = new CancellationTokenSource();
            _ = DebouncedGameLinkResolveAsync(value.Trim(), _gameLinkResolveSeq, _gameLinkDebounceCts.Token);
        }

        private async Task DebouncedGameLinkResolveAsync(string link, int sequence, CancellationToken token)
        {
            try
            {
                await Task.Delay(500, token);

                if (token.IsCancellationRequested || sequence != _gameLinkResolveSeq)
                    return;

                if (!LooksLikeRobloxLink(link))
                {
                    ResetLinkSelection();
                    LinkShortcutStatus = Strings.Menu_Shortcuts_InvalidLink;
                    return;
                }

                await ResolveGameLinkAsync(link, sequence, token);
            }
            catch (OperationCanceledException) { }
        }

        private async Task ResolveGameLinkAsync(string link, int sequence, CancellationToken token)
        {
            ResetLinkSelection();
            LinkShortcutStatus = Strings.Menu_Shortcuts_UpdatingPreview;

            string? command;

            try
            {
                command = await GameJoin.GetLaunchCommandByLink(link);
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested || sequence != _gameLinkResolveSeq)
                    return;

                App.Logger.Error($"Failed to resolve pasted game link: {ex.Message}");
                ResetLinkSelection();
                LinkShortcutStatus = Strings.Menu_Shortcuts_InvalidLink;
                return;
            }

            if (token.IsCancellationRequested || sequence != _gameLinkResolveSeq)
                return;

            if (string.IsNullOrEmpty(command))
            {
                ResetLinkSelection();
                LinkShortcutStatus = Strings.Menu_Shortcuts_InvalidLink;
                return;
            }

            long placeId = 0;
            string? jobId = null;
            string? accessCode = null;
            string? linkCode = null;

            if (Uri.TryCreate(command, UriKind.Absolute, out Uri? uri))
            {
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

                _ = long.TryParse(query["placeId"], NumberStyles.Integer, CultureInfo.InvariantCulture, out placeId);

                jobId = query["gameInstanceId"];
                accessCode = query["accessCode"];
                linkCode = query["linkCode"];
            }

            if (!IsValidJobId(jobId))
                jobId = null;

            if (placeId <= 0)
            {
                ResetLinkSelection();
                LinkShortcutStatus = Strings.Menu_Shortcuts_InvalidLink;
                return;
            }

            _linkPlaceId = placeId.ToString(CultureInfo.InvariantCulture);
            _linkJobId = jobId ?? string.Empty;
            _linkAccessCode = accessCode ?? linkCode ?? string.Empty;
            _linkAccessCodeIsLinkCode = string.IsNullOrEmpty(accessCode) && !string.IsNullOrEmpty(linkCode);

            SetPendingLinkPreview(placeId);

            await FetchLinkPreviewAsync(placeId, token);
        }

        private static bool IsValidJobId(string? jobId) =>
            !string.IsNullOrWhiteSpace(jobId) && Guid.TryParse(jobId.Trim(), out _);

        private void ResetLinkSelection()
        {
            _linkPlaceId = string.Empty;
            _linkJobId = string.Empty;
            _linkAccessCode = string.Empty;
            _linkAccessCodeIsLinkCode = false;
            LinkPreviewName = Strings.Menu_Shortcuts_NoGameSelected;
            LinkPreviewId = string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, 0);
            LinkPreviewIcon = null;
            LinkShortcutStatus = Strings.Menu_Shortcuts_Ready;
        }

        private void SetPendingLinkPreview(long placeId)
        {
            LinkPreviewName = string.Format(CultureInfo.CurrentCulture, Strings.Menu_Shortcuts_Game, placeId);
            LinkPreviewId = string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, placeId);
            LinkPreviewIcon = null;
            LinkShortcutStatus = Strings.Menu_Shortcuts_UpdatingPreview;
        }

        private async Task FetchLinkPreviewAsync(long placeId, CancellationToken token)
        {
            LinkShortcutStatus = Strings.Menu_Shortcuts_UpdatingPreview;

            GamePreviewResult? preview = await FetchGamePreviewAsync(placeId, token);

            if (preview is null)
                return;

            if (!preview.PlaceExists)
                _linkPlaceId = string.Empty;

            LinkPreviewName = preview.Name;
            LinkPreviewId = preview.Id;
            LinkPreviewIcon = preview.Icon;
            LinkShortcutStatus = preview.Status;
        }

        private async Task FetchInfoForId(long placeId, CancellationToken token)
        {
            ShortcutStatus = Strings.Menu_Shortcuts_UpdatingPreview;

            GamePreviewResult? preview = await FetchGamePreviewAsync(placeId, token);

            if (preview is null)
                return;

            if (!preview.PlaceExists)
                PlaceId = string.Empty;

            PreviewName = preview.Name;
            PreviewId = preview.Id;
            PreviewIcon = preview.Icon;
            ShortcutStatus = preview.Status;
        }

        private static async Task<GamePreviewResult?> FetchGamePreviewAsync(long placeId, CancellationToken token)
        {
            try
            {
                long? universeId;

                try
                {
                    universeId = await UniverseDetails.ResolveUniverseId(placeId);
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested)
                        return null;

                    App.Logger.Error($"Failed to resolve universe ID for place {placeId}: {ex.Message}");

                    return new GamePreviewResult(
                        string.Format(CultureInfo.CurrentCulture, Strings.Menu_Shortcuts_Game, placeId),
                        string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, placeId),
                        null,
                        Strings.Menu_Shortcuts_ReadyWithManualId,
                        true);
                }

                if (token.IsCancellationRequested)
                    return null;

                if (universeId is null)
                {
                    return new GamePreviewResult(
                        Strings.Menu_Shortcuts_NoGameSelected,
                        string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, 0),
                        null,
                        Strings.Menu_Shortcuts_NotFound,
                        false);
                }

                if (UniverseDetails.LoadFromCache(universeId.Value) is null)
                    await UniverseDetails.FetchSingle(universeId.Value);

                if (token.IsCancellationRequested)
                    return null;

                var details = UniverseDetails.LoadFromCache(universeId.Value);

                if (details != null)
                {
                    Bitmap? icon = await LoadBitmapFromUrl(details.Thumbnail.ImageUrl, token);

                    if (token.IsCancellationRequested)
                        return null;

                    return new GamePreviewResult(
                        details.Data.Name,
                        string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, placeId),
                        icon,
                        Strings.Menu_Shortcuts_ReadyToCreate,
                        true);
                }

                return new GamePreviewResult(
                    string.Format(CultureInfo.CurrentCulture, Strings.Menu_Shortcuts_Game, placeId),
                    string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, placeId),
                    null,
                    Strings.Menu_Shortcuts_ReadyWithManualId,
                    true);
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested)
                    return null;

                App.Logger.Error($"Failed to fetch info for place {placeId}: {ex.Message}");

                return new GamePreviewResult(
                    string.Format(CultureInfo.CurrentCulture, Strings.Menu_Shortcuts_Game, placeId),
                    string.Format(CultureInfo.CurrentCulture, Strings.Menu_RegionSelector_ID, placeId),
                    null,
                    Strings.Menu_Shortcuts_ReadyWithManualId,
                    true);
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
                        Size = ThumbnailSize.Large,
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
                                var response = await App.HttpClient.GetByteArrayAsync(new Uri(fetchedUrls[i]!));
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
                App.Logger.Error($"Search error: {ex.Message}");
            }
            finally
            {
                IsGameSearchLoading = false;
            }
        }

        private sealed record GamePreviewResult(string Name, string Id, Bitmap? Icon, string Status, bool PlaceExists);

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

                _gameLinkDebounceCts?.Cancel();
                _gameLinkDebounceCts?.Dispose();
                _gameLinkDebounceCts = null;
            }

            _disposed = true;
        }
    }
}
