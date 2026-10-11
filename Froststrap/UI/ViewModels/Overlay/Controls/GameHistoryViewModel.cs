using Avalonia.Threading;
using Froststrap.Enums;
using Froststrap.Integrations;
using Froststrap.Utility;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    internal class GameHistoryViewModel : NotifyPropertyChangedViewModel
    {
        private const int ThumbnailDecodeWidth = 112;

        private readonly ActivityWatcher? _activityWatcher;

        private bool _loaded;

        public BulkObservableCollection<ActivityData> GameHistory { get; } = new();

        private GenericTriState _loadState = GenericTriState.Unknown;
        public GenericTriState LoadState
        {
            get => _loadState;
            private set => SetProperty(ref _loadState, value);
        }

        private string _error = String.Empty;
        public string Error
        {
            get => _error;
            private set => SetProperty(ref _error, value);
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
        }

        public void SetVisible(bool visible)
        {
            if (visible && !_loaded)
                _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_activityWatcher is null)
                return;

            if (!_loaded)
                LoadState = GenericTriState.Unknown;

            try
            {
                List<ActivityData> snapshot = [.. _activityWatcher.History];

                var history = snapshot
                    .Where(x => x.UniverseId != 0 && x.TimeJoined > DateTime.Now.AddDays(-30))
                    .OrderByDescending(x => x.TimeJoined)
                    .Take(30)
                    .ToList();

                var ids = history.Select(x => x.UniverseId).Distinct().ToList();
                if (ids.Count > 0)
                    await UniverseDetails.FetchBulk(string.Join(',', ids));

                foreach (var entry in history)
                    entry.UniverseDetails ??= UniverseDetails.LoadFromCache(entry.UniverseId);

                await LoadThumbnailsAsync(history.Where(x => x.ThumbnailBitmap is null).ToList());

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    GameHistory.ReplaceAll(history);

                    Error = String.Empty;
                    LoadState = GenericTriState.Successful;
                    _loaded = true;
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

        private static async Task LoadThumbnailsAsync(List<ActivityData> targets)
        {
            if (targets.Count == 0)
                return;

            var requests = targets
                .Select(x => new ThumbnailRequest
                {
                    Type = ThumbnailType.GameIcon,
                    TargetId = (ulong)x.UniverseId,
                    Size = ThumbnailSize.Large
                })
                .ToList();

            var urls = await Thumbnails.GetThumbnailUrlsAsync(requests, CancellationToken.None);

            await Task.WhenAll(targets.Select(async (target, index) =>
            {
                string? url = urls.ElementAtOrDefault(index);

                if (String.IsNullOrEmpty(url))
                    return;

                try
                {
                    target.ThumbnailBitmap = await ImageLoader.LoadAsync(url, ThumbnailDecodeWidth);
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Failed to load history thumbnail: {ex.Message}");
                }
            }));
        }
    }
}