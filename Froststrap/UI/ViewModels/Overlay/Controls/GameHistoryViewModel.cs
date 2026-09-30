using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Froststrap.Enums;
using Froststrap.UI.ViewModels;
using Froststrap.Integrations;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    internal class GameHistoryViewModel : NotifyPropertyChangedViewModel
    {
        private readonly ActivityWatcher? _activityWatcher;

        public ObservableCollection<ActivityData> GameHistory { get; } = [];

        private GenericTriState _loadState = GenericTriState.Unknown;
        public GenericTriState LoadState
        {
            get => _loadState;
            private set => Set(ref _loadState, value);
        }

        private string _error = String.Empty;
        public string Error
        {
            get => _error;
            private set => Set(ref _error, value);
        }

        public GameHistoryViewModel(ActivityWatcher? activityWatcher)
        {
            _activityWatcher = activityWatcher;

            if (_activityWatcher is null)
            {
                LoadState = GenericTriState.Successful;
                return;
            }

            _activityWatcher.OnGameLeave += (_, _) => _ = LoadAsync();
            _activityWatcher.OnHistoryUpdated += (_, _) => _ = LoadAsync();

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_activityWatcher is null)
                return;

            LoadState = GenericTriState.Unknown;

            try
            {
                var history = _activityWatcher.History
                    .Where(x => x.UniverseId != 0 && x.TimeJoined > DateTime.Now.AddDays(-30))
                    .OrderByDescending(x => x.TimeJoined)
                    .Take(30)
                    .ToList();

                var ids = history.Select(x => x.UniverseId).Distinct().ToList();
                if (ids.Count > 0)
                    await UniverseDetails.FetchBulk(string.Join(',', ids));

                foreach (var entry in history)
                {
                    if (entry.UniverseDetails is null)
                        entry.UniverseDetails = UniverseDetails.LoadFromCache(entry.UniverseId);
                }

                // Batch-fetch thumbnails for entries that don't have a bitmap yet
                var targets = history.Where(x => x.ThumbnailBitmap is null).ToList();
                if (targets.Count > 0)
                {
                    var thumbRequests = targets
                        .Select(x => new ThumbnailRequest
                        {
                            Type = ThumbnailType.GameIcon,
                            TargetId = (ulong)x.UniverseId,
                            Size = ThumbnailSize.Large
                        })
                        .ToList();

                    var urls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, CancellationToken.None);

                    for (int i = 0; i < targets.Count && i < urls.Length; i++)
                    {
                        string? url = urls[i];
                        if (string.IsNullOrEmpty(url))
                            continue;

                        try
                        {
                            byte[] bytes = await App.HttpClient.GetByteArrayAsync(new Uri(url));
                            using var ms = new MemoryStream(bytes);
                            targets[i].ThumbnailBitmap = new Bitmap(ms);
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Error($"Failed to load history thumbnail: {ex.Message}");
                        }
                    }
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    GameHistory.Clear();
                    foreach (ActivityData entry in history)
                        GameHistory.Add(entry);

                    Error = String.Empty;
                    LoadState = GenericTriState.Successful;
                });
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load game history", ex);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Error = ex.Message;
                    LoadState = GenericTriState.Failed;
                });
            }
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            OnPropertyChanged(name);
        }
    }
}
