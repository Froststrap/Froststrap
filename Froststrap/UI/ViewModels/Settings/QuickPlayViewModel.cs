using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    public record PrivateServerInfo(
        long VipServerId,
        string AccessCode,
        string Name,
        long OwnerId,
        string OwnerName,
        string? OwnerAvatarUrl,
        int MaxPlayers,
        int CurrentPlayers);

    public class QuickPlayViewModel : NotifyPropertyChangedViewModel
    {
        private bool _isLoading;
        private bool _isOverlayVisible;
        private bool _isSubplacesOverlayVisible;
        private bool _isLoadingSubplaces;
        private readonly ObservableCollection<PlaceInfo> _subplaces = [];
        private UniverseDetails? _selectedUniverseDetails;
        private readonly string _cachePath = Path.Combine(Paths.Cache, "GameHistory.json");
        private List<GameHistoryEntry> _allHistory = [];

        private bool _isPrivateServersOverlayVisible;
        private bool _arePrivateServersEmpty;
        private bool _isLoadingPrivateServers;
        private long _currentPrivateServersPlaceId;

        private readonly ObservableCollection<PrivateServerInfo> _privateServers = [];

        public ObservableCollection<QuickPlayGameItem> RecentGames { get; } = [];
        public ObservableCollection<ServerInfo> SelectedGameServers { get; } = [];

        public UniverseDetails? SelectedUniverseDetails
        {
            get => _selectedUniverseDetails;
            set => SetProperty(ref _selectedUniverseDetails, value);
        }

        public bool IsOverlayVisible
        {
            get => _isOverlayVisible;
            set => SetProperty(ref _isOverlayVisible, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsSubplacesOverlayVisible
        {
            get => _isSubplacesOverlayVisible;
            set => SetProperty(ref _isSubplacesOverlayVisible, value);
        }

        public bool IsLoadingSubplaces
        {
            get => _isLoadingSubplaces;
            set => SetProperty(ref _isLoadingSubplaces, value);
        }

        public bool IsPrivateServersOverlayVisible
        {
            get => _isPrivateServersOverlayVisible;
            set => SetProperty(ref _isPrivateServersOverlayVisible, value);
        }

        public bool ArePrivateServersEmpty
        {
            get => _arePrivateServersEmpty;
            set => SetProperty(ref _arePrivateServersEmpty, value);
        }

        public bool IsLoadingPrivateServers
        {
            get => _isLoadingPrivateServers;
            set => SetProperty(ref _isLoadingPrivateServers, value);
        }

        public ObservableCollection<PlaceInfo> Subplaces => _subplaces;
        public ObservableCollection<PrivateServerInfo> PrivateServers => _privateServers;

        public static bool HasActiveAccount => AccountManager.Shared?.ActiveAccount != null;

        public ICommand JoinGameCommand { get; }
        public ICommand RejoinLastServerCommand { get; }
        public ICommand ViewServersCommand { get; }
        public ICommand CloseOverlayCommand { get; }
        public ICommand CloseSubplacesCommand { get; }
        public ICommand VisitPageCommand { get; }
        public ICommand ViewSubplacesCommand { get; }
        public ICommand JoinSubplaceCommand { get; }
        public ICommand ShowPrivateServersCommand { get; }
        public ICommand JoinPrivateServerCommand { get; }
        public ICommand ClosePrivateServersCommand { get; }

        public QuickPlayViewModel()
        {
            JoinGameCommand = new RelayCommand<QuickPlayGameItem>(item =>
            {
                if (item != null) LaunchRoblox(item.PlaceId);
            });

            RejoinLastServerCommand = new RelayCommand<object>(param =>
            {
                if (param is QuickPlayGameItem item)
                {
                    LaunchRoblox(item.PlaceId, item.LastJobId);
                }
                else if (param is ServerInfo server)
                {
                    var entry = _allHistory.FirstOrDefault(x => x.UniverseId == SelectedUniverseDetails?.Data?.Id);
                    if (entry != null) LaunchRoblox(entry.PlaceId, server.JobId);
                }
            });

            ViewSubplacesCommand = new RelayCommand<QuickPlayGameItem>(async item =>
            {
                if (item == null || item.UniverseId == 0) return;

                SelectedUniverseDetails = item.OriginalDetails;
                IsSubplacesOverlayVisible = true;
                await FetchSubplacesAsync(item.UniverseId);
            });

            JoinSubplaceCommand = new RelayCommand<PlaceInfo>(subplace =>
            {
                if (subplace != null) LaunchRoblox(subplace.Id);
            });

            ViewServersCommand = new RelayCommand<QuickPlayGameItem>(item =>
            {
                if (item == null) return;

                var entry = _allHistory.FirstOrDefault(x => x.UniverseId == item.UniverseId);
                if (entry == null) return;

                var sortedServers = entry.Servers.OrderByDescending(x => x.JoinedAt).ToList();

                foreach (var s in sortedServers) s.IsLatest = false;
                if (sortedServers.Count > 0) sortedServers[0].IsLatest = true;

                SelectedGameServers.Clear();
                foreach (var s in sortedServers) SelectedGameServers.Add(s);

                SelectedUniverseDetails = item.OriginalDetails;
                IsOverlayVisible = true;
            });

            CloseOverlayCommand = new RelayCommand(() => IsOverlayVisible = false);
            CloseSubplacesCommand = new RelayCommand(() => IsSubplacesOverlayVisible = false);

            VisitPageCommand = new RelayCommand<QuickPlayGameItem>(item =>
            {
                if (item != null) Process.Start(new ProcessStartInfo($"https://www.roblox.com/games/{item.PlaceId}") { UseShellExecute = true });
            });

            ShowPrivateServersCommand = new RelayCommand<QuickPlayGameItem>(async item =>
            {
                if (item == null || item.PlaceId == 0) return;
                _currentPrivateServersPlaceId = item.PlaceId;
                await ShowPrivateServersForGameAsync();
            });

            JoinPrivateServerCommand = new RelayCommand<string>(accessCode =>
            {
                if (string.IsNullOrWhiteSpace(accessCode)) return;
                LaunchRoblox(_currentPrivateServersPlaceId, accessCode: accessCode);
                IsPrivateServersOverlayVisible = false;
            });

            ClosePrivateServersCommand = new RelayCommand(() => IsPrivateServersOverlayVisible = false);

            AccountManager.Shared.ActiveAccountChanged += _ =>
            {
                Dispatcher.UIThread.InvokeAsync(() => OnPropertyChanged(nameof(HasActiveAccount)));
            };

            _ = Initialize();
        }

        private async Task Initialize()
        {
            IsLoading = true;
            _allHistory = LoadLocalHistory(_cachePath);

            if (_allHistory.Count == 0)
            {
                IsLoading = false;
                return;
            }

            var universeIds = _allHistory
                .Select(x => x.UniverseId)
                .Where(id => id > 0)
                .Distinct()
                .Select(id => id.ToString())
                .ToList();

            if (universeIds.Count > 0)
            {
                await UniverseDetails.FetchBulk(string.Join(",", universeIds));
            }
            else
            {
                App.Logger.WriteLine("QuickPlayViewModel", "No valid Universe IDs found in history.");
            }

            var uiItems = new List<QuickPlayGameItem>();
            foreach (var entry in _allHistory)
            {
                var details = UniverseDetails.LoadFromCache(entry.UniverseId);
                var lastSession = entry.Servers.OrderByDescending(s => s.JoinedAt).FirstOrDefault();

                uiItems.Add(new QuickPlayGameItem
                {
                    UniverseId = entry.UniverseId,
                    PlaceId = entry.PlaceId,
                    Name = details?.Data?.Name ?? "Unknown Game",
                    Creator = details?.Data?.Creator?.Name ?? "Unknown",
                    Playing = details?.Data?.Playing ?? 0,
                    Visits = details?.Data?.Visits ?? 0,
                    ServerCount = entry.Servers.Count,
                    LastJobId = lastSession?.JobId,
                    OriginalDetails = details
                });
            }

            var thumbRequests = uiItems.Select(item => new ThumbnailRequest
            {
                TargetId = (ulong)item.UniverseId,
                Type = ThumbnailType.GameIcon,
                Size = "150x150",
                Format = ThumbnailFormat.Png
            }).ToList();

            try
            {
                var urls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, CancellationToken.None);
                for (int i = 0; i < uiItems.Count; i++)
                {
                    string? thumbUrl = urls.ElementAtOrDefault(i);
                    if (string.IsNullOrEmpty(thumbUrl)) continue;
                    uiItems[i].ThumbnailUrl = thumbUrl;
                    var details = uiItems[i].OriginalDetails;
                    if (details?.Thumbnail != null)
                        details.Thumbnail.ImageUrl = thumbUrl;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Thumbnail fetch failed: {ex.Message}"); }

            RecentGames.Clear();
            foreach (var item in uiItems) RecentGames.Add(item);
            IsLoading = false;
        }

        private static List<GameHistoryEntry> LoadLocalHistory(string cachePath)
        {
            try
            {
                if (!File.Exists(cachePath)) return [];
                string json = File.ReadAllText(cachePath);
                return JsonSerializer.Deserialize<List<GameHistoryEntry>>(json) ?? [];
            }
            catch { return []; }
        }

        private async Task FetchSubplacesAsync(long universeId)
        {
            try
            {
                IsLoadingSubplaces = true;
                Subplaces.Clear();

                Uri url = UrlBuilder.BuildApiUrl(
                    "develop",
                    $"v1/universes/{universeId}/places?isUniverseCreation=false&limit=100&sortOrder=Asc"
                );

                var subplacesResponse = await Http.GetJson<SubplacesResponse>(url);

                if (subplacesResponse?.Data != null && subplacesResponse.Data.Count > 0)
                {
                    var tempSubplaces = subplacesResponse.Data
                        .Select(place => new PlaceInfo(place.Id, place.UniverseId, place.Name, ""))
                        .ToList();

                    var thumbRequests = tempSubplaces.Select(p => new ThumbnailRequest
                    {
                        TargetId = (ulong)p.Id,
                        Type = ThumbnailType.PlaceIcon,
                        Size = "150x150",
                        Format = ThumbnailFormat.Png
                    }).ToList();

                    try
                    {
                        var urls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, CancellationToken.None);
                        for (int i = 0; i < tempSubplaces.Count; i++)
                        {
                            tempSubplaces[i].ThumbnailUrl = urls.ElementAtOrDefault(i) ?? "";
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine("QuickPlayViewModel", $"Subplace thumbnail fetch failed: {ex.Message}");
                    }

                    foreach (var p in tempSubplaces) Subplaces.Add(p);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("QuickPlayViewModel", $"Subplace fetch failed: {ex.Message}");
            }
            finally
            {
                IsLoadingSubplaces = false;
            }
        }

        private async Task ShowPrivateServersForGameAsync()
        {
            if (_currentPrivateServersPlaceId == 0) return;

            var accountManager = AccountManager.Shared;
            if (accountManager is null)
            {
                _ = Frontend.ShowMessageBox("Account manager is not available.", MessageBoxImage.Error);
                return;
            }

            var activeAccount = accountManager.ActiveAccount;
            if (activeAccount == null)
            {
                _ = Frontend.ShowMessageBox("Please select an account first.", MessageBoxImage.Warning);
                return;
            }

            IsLoadingPrivateServers = true;
            IsPrivateServersOverlayVisible = true;
            PrivateServers.Clear();
            ArePrivateServersEmpty = false;

            try
            {
                string? cookie = accountManager.GetRoblosecurityForUser(activeAccount.UserId);
                if (string.IsNullOrEmpty(cookie))
                {
                    _ = Frontend.ShowMessageBox("Unable to authenticate. Please log in again.", MessageBoxImage.Warning);
                    return;
                }

                Uri url = UrlBuilder.BuildApiUrl(
                    "games",
                    $"v1/games/{_currentPrivateServersPlaceId}/private-servers?excludeFriendServers=false&sortOrder=Asc"
                );

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
                request.Headers.Add("Origin", "https://www.roblox.com");
                request.Headers.Add("Referrer", "https://www.roblox.com");

                var response = await Http.SendJson<PrivateServersResponse>(request);
                if (response?.Data == null || response.Data.Count == 0)
                {
                    ArePrivateServersEmpty = true;
                    return;
                }

                var ownerIds = response.Data
                    .Select(s => s.Owner.Id)
                    .Where(id => id != 0)
                    .Distinct()
                    .ToList();

                var avatarUrls = new Dictionary<long, string?>();
                if (ownerIds.Count > 0)
                {
                    var results = await accountManager.GetAvatarUrlsBulkAsync(ownerIds);
                    avatarUrls = results;
                }

                var servers = new List<PrivateServerInfo>();
                foreach (var server in response.Data)
                {
                    string? avatarUrl = avatarUrls.GetValueOrDefault(server.Owner.Id);
                    servers.Add(new PrivateServerInfo(
                        server.VipServerId,
                        server.AccessCode,
                        server.Name,
                        server.Owner.Id,
                        server.Owner.Name,
                        avatarUrl,
                        server.MaxPlayers,
                        server.Players.Count
                    ));
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var server in servers)
                        PrivateServers.Add(server);
                    ArePrivateServersEmpty = servers.Count == 0;
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("QuickPlayViewModel", $"Exception in ShowPrivateServersForGameAsync: {ex.Message}");
                await Dispatcher.UIThread.InvokeAsync(() => ArePrivateServersEmpty = true);
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsLoadingPrivateServers = false);
            }
        }

        private static void LaunchRoblox(long placeId, string? jobId = null, string? accessCode = null)
        {
            if (placeId == 0) return;

            string baseUrl = "roblox://experiences/start";
            string deeplink = $"{baseUrl}?placeId={placeId}";

            if (!string.IsNullOrEmpty(accessCode))
            {
                deeplink += "&accessCode=" + Uri.EscapeDataString(accessCode);
            }
            else if (!string.IsNullOrEmpty(jobId))
            {
                deeplink += "&gameInstanceId=" + Uri.EscapeDataString(jobId);
            }

            Process.Start(new ProcessStartInfo(deeplink) { UseShellExecute = true });
        }
    }
}