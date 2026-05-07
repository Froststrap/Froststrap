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
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using Avalonia.Media.Imaging;

namespace Froststrap.UI.ViewModels.Settings
{
    public class ShortcutsViewModel : NotifyPropertyChangedViewModel
    {
        private readonly string LOG_IDENT = "ShortcutsViewModel";

        public ShortcutTask DesktopIconTask { get; } = new("Desktop", Paths.Desktop, $"{App.ProjectName}.lnk");
        public ShortcutTask PlayerIconTask { get; } = new("RobloxPlayer", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRoblox}.lnk", "-player");
        public ShortcutTask StudioIconTask { get; } = new("RobloxStudio", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRobloxStudio}.lnk", "-studio");
        public ShortcutTask SettingsIconTask { get; } = new("Settings", Paths.Desktop, $"{Strings.Menu_Title}.lnk", "-settings");
        public ShortcutTask AccountManagerIconTask { get; } = new("AccountManager", Paths.Desktop, "Account Manager.lnk", "-accountmanager");
        public ExtractIconsTask ExtractIconsTask { get; } = new();

        #region Fields
        private string _searchQuery = "";
        private bool _isGameSearchLoading;
        private string _placeId = "";
        private string _jobId = "";
        private string _accessCode = "";
        private string _previewName = "No Game Selected";
        private string _previewId = "ID: 0";
        private Bitmap? _previewIcon;
        private string _shortcutStatus = "Ready";
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
            CreateGameShortcutCommand = new AsyncRelayCommand(CreateGameShortcut);
            SearchGamesCommand = new AsyncRelayCommand(SearchGamesAsync);
        }

        private async Task CreateGameShortcut()
        {
            if (string.IsNullOrEmpty(PlaceId) || PreviewName == "No Game Selected")
            {
                ShortcutStatus = "Select a game first.";
                return;
            }

            try
            {
                ShortcutStatus = "Processing...";

                string argData = PlaceId;
                if (!string.IsNullOrEmpty(JobId)) argData += $";{JobId}";
                if (!string.IsNullOrEmpty(AccessCode)) argData += $";{AccessCode}";

                string safeName = SanitizeFileName(PreviewName);
                string lnkPath = Path.Combine(Paths.Desktop, $"{safeName}.lnk");

                string shortcutsIconDir = Path.Combine(Paths.Cache, "Game Shortcuts");
                Directory.CreateDirectory(shortcutsIconDir);

                string? finalIconPath = null;

                if (PreviewIcon != null)
                {
                    try
                    {
                        ShortcutStatus = "Saving icon...";
                        using var ms = new MemoryStream();
                        PreviewIcon.Save(ms);
                        byte[] imageBytes = ms.ToArray();

                        string hash = ComputeHash(imageBytes);
                        string icoPath = Path.Combine(shortcutsIconDir, $"{hash}.ico");

                        if (!File.Exists(icoPath))
                        {
                            ShortcutStatus = "Converting icon...";
                            using var icoFile = File.Create(icoPath);
                            SaveBitmapAsIcon(PreviewIcon, icoFile);
                        }
                        finalIconPath = icoPath;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Icon processing failed: {ex.Message}");
                    }
                }

                ShortcutStatus = "Creating...";
                Shortcut.Create(Paths.Application, $"-gameshortcut \"{argData}\"", lnkPath, finalIconPath);
                ShortcutStatus = "Shortcut created!";
            }
            catch (Exception ex)
            {
                ShortcutStatus = "Error creating shortcut.";
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
            PreviewId = $"ID: {value.RootPlaceId}";
            PreviewIcon = value.ThumbnailBitmap;
            ShortcutStatus = "Ready to create";

            _isProcessingSelection = false;
            IsSearchFlyoutOpen = false;
        }

        private async Task FetchInfoForId(long id, CancellationToken token)
        {
            try
            {
                ShortcutStatus = "Updating preview...";

                await UniverseDetails.FetchBulk(id.ToString());
                var details = UniverseDetails.LoadFromCache(id);

                if (details != null)
                {
                    PreviewName = details.Data.Name;
                    PreviewId = $"ID: {id}";
                    PreviewIcon = await LoadBitmapFromUrl(details.Thumbnail.ImageUrl, token);
                }
                else
                {
                    PreviewName = $"Game {id}";
                    PreviewId = $"ID: {id}";
                    PreviewIcon = null;
                }
            }
            catch (Exception)
            {
                PreviewName = $"Game {id}";
                ShortcutStatus = "Ready with manual ID.";
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

        private static void SaveBitmapAsIcon(Bitmap bitmap, Stream output)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms);
            ms.Position = 0;

            using var resized = Bitmap.DecodeToWidth(ms, 64);
            using var pngStream = new MemoryStream();
            resized.Save(pngStream);
            byte[] pngBytes = pngStream.ToArray();

            using var writer = new BinaryWriter(output);
            writer.Write((short)0);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write((byte)64);
            writer.Write((byte)64);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write(pngBytes.Length);
            writer.Write(22);
            writer.Write(pngBytes);
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        private static string ComputeHash(byte[] data)
        {
            byte[] hash = SHA256.HashData(data);
            return Convert.ToHexStringLower(hash);
        }
    }
}