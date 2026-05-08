using CommunityToolkit.Mvvm.Input;
using Froststrap.Models.APIs.Roblox;
using Froststrap.Models.Entities;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    public class QuickPlayViewModel : NotifyPropertyChangedViewModel
    {
        private bool _isLoading;
        private readonly string _cachePath = Path.Combine(Paths.Cache, "GameHistory.json");

        public ObservableCollection<ActivityData> RecentGames { get; } = [];

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand JoinGameCommand { get; }
        public ICommand RejoinLastServerCommand { get; }
        public ICommand VisitPageCommand { get; }

        public QuickPlayViewModel()
        {
            JoinGameCommand = new RelayCommand<ActivityData>(g => { if (g != null) LaunchRoblox(g.PlaceId); });
            RejoinLastServerCommand = new RelayCommand<ActivityData>(g => { if (g != null) LaunchRoblox(g.PlaceId, g.JobId); });
            VisitPageCommand = new RelayCommand<ActivityData>(g =>
            {
                if (g != null) Process.Start(new ProcessStartInfo($"https://www.roblox.com/games/{g.PlaceId}") { UseShellExecute = true });
            });

            _ = Initialize();
        }

        private async Task Initialize()
        {
            IsLoading = true;

            var history = LoadLocalHistory();
            if (history.Count == 0)
            {
                IsLoading = false;
                return;
            }

            var uniqueGames = history
                .GroupBy(x => x.UniverseId)
                .Select(g => g.First())
                .ToList();

            var idsToFetch = uniqueGames
                .Where(g => UniverseDetails.LoadFromCache(g.UniverseId) == null)
                .Select(g => g.UniverseId.ToString())
                .ToList();

            if (idsToFetch.Count != 0)
            {
                await UniverseDetails.FetchBulk(string.Join(",", idsToFetch));
            }

            var thumbRequests = uniqueGames.Select(g => new ThumbnailRequest
            {
                TargetId = (ulong)g.UniverseId,
                Type = ThumbnailType.GameIcon,
                Size = "150x150",
                Format = ThumbnailFormat.Png
            }).ToList();

            try
            {
                var urls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, CancellationToken.None);

                for (int i = 0; i < uniqueGames.Count; i++)
                {
                    var game = uniqueGames[i];
                    game.UniverseDetails ??= UniverseDetails.LoadFromCache(game.UniverseId);

                    if (game.UniverseDetails != null && urls[i] != null)
                    {
                        game.UniverseDetails.Thumbnail.ImageUrl = urls[i];
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Thumbnail fetch failed: {ex.Message}");
            }

            RecentGames.Clear();
            foreach (var game in uniqueGames)
            {
                RecentGames.Add(game);
            }

            IsLoading = false;
        }

        private List<ActivityData> LoadLocalHistory()
        {
            try
            {
                if (!File.Exists(_cachePath)) return [];

                string json = File.ReadAllText(_cachePath);
                var gameHistory = JsonSerializer.Deserialize<List<GameHistoryEntry>>(json);

                if (gameHistory == null) return [];

                var loadedHistory = new List<ActivityData>();

                foreach (var entry in gameHistory)
                {
                    if (entry.UniverseId == 0 || entry.PlaceId == 0) continue;

                    foreach (var server in entry.Servers)
                    {
                        if (server.JoinedAt == default) continue;

                        loadedHistory.Add(new ActivityData
                        {
                            UniverseId = entry.UniverseId,
                            PlaceId = entry.PlaceId,
                            JobId = server.JobId,
                            ServerType = server.ServerType,
                            TimeJoined = server.JoinedAt,
                            TimeLeft = server.TimeLeft,
                            Region = server.Region
                        });
                    }
                }

                return [.. loadedHistory.OrderByDescending(x => x.TimeJoined)];
            }
            catch
            {
                return [];
            }
        }

        private static void LaunchRoblox(long placeId, string? jobId = null)
        {
            string url = string.IsNullOrEmpty(jobId)
                ? $"roblox://experiences/start?placeId={placeId}"
                : $"roblox://experiences/start?placeId={placeId}&gameInstanceId={jobId}";

            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
    }
}