using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
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

        public ObservableCollection<PlaceInfo> Subplaces => _subplaces;

        public ICommand JoinGameCommand { get; }
        public ICommand RejoinLastServerCommand { get; }
        public ICommand ViewServersCommand { get; }
        public ICommand CloseOverlayCommand { get; }
        public ICommand CloseSubplacesCommand { get; }
        public ICommand VisitPageCommand { get; }
        public ICommand ViewSubplacesCommand { get; }
        public ICommand JoinSubplaceCommand { get; }

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

            _ = Initialize();
        }

        private async Task Initialize()
        {
            IsLoading = true;
            _allHistory = LoadLocalHistory();

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
                    if (uiItems[i].OriginalDetails?.Thumbnail != null)
                        uiItems[i].OriginalDetails!.Thumbnail.ImageUrl = thumbUrl;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Thumbnail fetch failed: {ex.Message}"); }

            RecentGames.Clear();
            foreach (var item in uiItems) RecentGames.Add(item);
            IsLoading = false;
        }

        private List<GameHistoryEntry> LoadLocalHistory()
        {
            try
            {
                if (!File.Exists(_cachePath)) return [];
                string json = File.ReadAllText(_cachePath);
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

                using var client = new HttpClient();
                string url = $"https://develop.roblox.com/v1/universes/{universeId}/places?isUniverseCreation=false&limit=100&sortOrder=Asc";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return;

                var responseContent = await response.Content.ReadAsStringAsync();
                var subplacesResponse = JsonSerializer.Deserialize<SubplacesResponse>(responseContent);

                if (subplacesResponse?.Data != null && subplacesResponse.Data.Count > 0)
                {
                    var tempSubplaces = subplacesResponse.Data.Select(place => new PlaceInfo(place.Id, place.UniverseId, place.Name, "")).ToList();

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
                    catch (Exception ex) { Debug.WriteLine($"Subplace thumbnail fetch failed: {ex.Message}"); }

                    foreach (var p in tempSubplaces) Subplaces.Add(p);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Subplace fetch failed: {ex.Message}"); }
            finally
            {
                IsLoadingSubplaces = false;
            }
        }

        private static void LaunchRoblox(long placeId, string? jobId = null)
        {
            if (placeId == 0) return;
            string url = string.IsNullOrEmpty(jobId)
                ? $"roblox://experiences/start?placeId={placeId}"
                : $"roblox://experiences/start?placeId={placeId}&gameInstanceId={jobId}";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
    }
}