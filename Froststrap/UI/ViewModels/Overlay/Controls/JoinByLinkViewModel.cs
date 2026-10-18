// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Froststrap.RobloxInterfaces;
using System.Collections.Specialized;
using System.Web;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    /// <summary>
    /// Resolves a pasted Roblox link, shows the experience it points at and joins it.
    /// </summary>
    internal class JoinByLinkViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private const int IconDecodeWidth = 128;
        private static readonly TimeSpan ResolveDelay = TimeSpan.FromMilliseconds(400);

        private readonly RelayCommand _join;
        private CancellationTokenSource? _resolveCts;

        private long _placeId;
        private string? _launchQuery;
        private string? _rawCommand;

        private bool _disposed;

        public ICommand OpenCommand { get; }
        public ICommand JoinCommand { get; }

        public JoinByLinkViewModel()
        {
            _join = new RelayCommand(Join, () => CanJoin);

            OpenCommand = new RelayCommand(Toggle);
            JoinCommand = _join;
        }

        private void Toggle() => IsOpen = !IsOpen;

        #region Popup and link

        private bool _isOpen;
        public bool IsOpen
        {
            get => _isOpen;
            set
            {
                if (!SetProperty(ref _isOpen, value))
                    return;

                if (value)
                    Show(null);
                else
                    CancelResolve();
            }
        }

        private string _link = String.Empty;
        public string Link
        {
            get => _link;
            set
            {
                if (SetProperty(ref _link, value))
                    _ = ResolveAsync(value);
            }
        }

        private bool _isResolving;
        public bool IsResolving
        {
            get => _isResolving;
            private set
            {
                if (SetProperty(ref _isResolving, value))
                    NotifyJoin();
            }
        }

        #endregion

        #region Resolved experience

        private string _gameName = String.Empty;
        public string GameName
        {
            get => _gameName;
            private set
            {
                if (SetProperty(ref _gameName, value))
                    OnPropertyChanged(nameof(HasGame));
            }
        }

        private string _gameCreator = String.Empty;
        public string GameCreator
        {
            get => _gameCreator;
            private set
            {
                if (SetProperty(ref _gameCreator, value))
                    OnPropertyChanged(nameof(HasCreator));
            }
        }

        private bool _isVerified;
        public bool IsVerified
        {
            get => _isVerified;
            private set => SetProperty(ref _isVerified, value);
        }

        private long _playing;
        public long Playing
        {
            get => _playing;
            private set
            {
                if (SetProperty(ref _playing, value))
                    OnPropertyChanged(nameof(HasPlaying));
            }
        }

        private Bitmap? _gameBitmap;
        public Bitmap? GameBitmap
        {
            get => _gameBitmap;
            private set => SetProperty(ref _gameBitmap, value);
        }

        public bool HasGame => _gameName.Length > 0;

        public bool HasCreator => _gameCreator.Length > 0;

        public bool HasPlaying => _playing > 0;

        #endregion

        #region Status

        private string _status = String.Empty;
        public string Status => _status;

        public bool HasStatus => _status.Length > 0;

        private void Show(string? status)
        {
            _status = status ?? String.Empty;

            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(HasStatus));
        }

        #endregion

        public bool CanJoin => !IsResolving && (_launchQuery is not null || _rawCommand is not null);

        private void NotifyJoin() => _join.NotifyCanExecuteChanged();

        private void CancelResolve()
        {
#pragma warning disable CA1849
            _resolveCts?.Cancel();
#pragma warning restore CA1849
            _resolveCts?.Dispose();
            _resolveCts = null;
        }

        private async Task ResolveAsync(string value)
        {
            CancelResolve();
            ClearResolved();

            if (String.IsNullOrWhiteSpace(value))
                return;

            var cts = new CancellationTokenSource();
            _resolveCts = cts;

            CancellationToken token = cts.Token;

            try
            {
                await Task.Delay(ResolveDelay, token);
                await ResolveCoreAsync(value.Trim(), token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to resolve the pasted link");
                App.Logger.Error(ex);

                Show(Strings.Menu_Overlay_JoinByLink_Failed);
            }
        }

        private async Task ResolveCoreAsync(string link, CancellationToken token)
        {
            IsResolving = true;
            Show(Strings.Menu_Overlay_JoinByLink_Resolving);

            try
            {
                string? command = await GameJoin.GetLaunchCommandByLink(link);

                if (token.IsCancellationRequested)
                    return;

                if (String.IsNullOrEmpty(command))
                {
                    Show(Strings.Menu_Overlay_JoinByLink_Invalid);
                    return;
                }

                long placeId = 0;
                string? jobId = null;
                string? accessCode = null;
                string? linkCode = null;

                if (Uri.TryCreate(command, UriKind.Absolute, out Uri? uri))
                {
                    NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);

                    if (long.TryParse(query["placeId"], NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) && parsed > 0)
                        placeId = parsed;

                    jobId = query["gameInstanceId"];
                    accessCode = query["accessCode"];
                    linkCode = query["linkCode"];
                }

                if (!IsValidJobId(jobId))
                    jobId = null;

                if (placeId == 0)
                {
                    // share links carry no place id, but Roblox can still launch them as they are
                    if (IsProtocolLink(command))
                    {
                        _rawCommand = command;
                        Show(null);
                        return;
                    }

                    Show(Strings.Menu_Overlay_JoinByLink_Invalid);
                    return;
                }

                _placeId = placeId;
                _launchQuery = BuildLaunchQuery(uri, placeId, jobId, accessCode, linkCode);

                await LoadGameInfoAsync(placeId, token);
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    IsResolving = false;
                    NotifyJoin();
                }
            }
        }

        private static string BuildLaunchQuery(Uri? uri, long placeId, string? jobId, string? accessCode, string? linkCode)
        {
            // launching the start link verbatim keeps extra parameters such as launch data
            if (uri is not null
                && uri.Scheme.Equals("roblox", StringComparison.OrdinalIgnoreCase)
                && uri.Host.Equals("experiences", StringComparison.OrdinalIgnoreCase)
                && uri.AbsolutePath.Trim('/').Equals("start", StringComparison.OrdinalIgnoreCase)
                && uri.Query.Length > 1)
                return uri.Query[1..];

            string query = $"placeId={placeId}";

            if (!String.IsNullOrEmpty(accessCode))
                query += $"&accessCode={Uri.EscapeDataString(accessCode)}";
            else if (!String.IsNullOrEmpty(jobId))
                query += $"&gameInstanceId={Uri.EscapeDataString(jobId)}";
            else if (!String.IsNullOrEmpty(linkCode))
                query += $"&linkCode={Uri.EscapeDataString(linkCode)}";

            return query;
        }

        private async Task LoadGameInfoAsync(long placeId, CancellationToken token)
        {
            try
            {
                Uri url = UrlBuilder.BuildApiUrl("apis", $"universes/v1/places/{placeId}/universe");

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await App.HttpClient.SendAsync(request, token);

                if (!response.IsSuccessStatusCode)
                {
                    Show(Strings.Menu_Overlay_JoinByLink_Failed);
                    return;
                }

                string body = await response.Content.ReadAsStringAsync(token);
                UniverseIdResponse? universe = JsonSerializer.Deserialize<UniverseIdResponse>(body);

                if (universe is null || universe.UniverseId == 0)
                {
                    Show(Strings.Menu_Overlay_JoinByLink_Failed);
                    return;
                }

                await UniverseDetails.FetchBulk(universe.UniverseId.ToString(CultureInfo.InvariantCulture));

                if (token.IsCancellationRequested)
                    return;

                UniverseDetails? details = UniverseDetails.LoadFromCache(universe.UniverseId);

                GameName = details?.Data?.Name ?? String.Empty;
                GameCreator = details?.Data?.Creator?.Name ?? String.Empty;
                IsVerified = details?.Data?.Creator?.HasVerifiedBadge ?? false;
                Playing = details?.Data?.Playing ?? 0;

                string? icon = details?.Thumbnail?.ImageUrl;

                if (!String.IsNullOrEmpty(icon))
                    _ = ImageLoader.LoadAndSetAsync(icon, IconDecodeWidth, b => GameBitmap = b);

                if (GameName.Length == 0)
                    GameName = Strings.Menu_QuickPlay_UnknownGame;

                Show(null);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to look up the experience for place {placeId}");
                App.Logger.Error(ex);

                Show(Strings.Menu_Overlay_JoinByLink_Failed);
            }
        }

        private void ClearResolved()
        {
            _placeId = 0;
            _launchQuery = null;
            _rawCommand = null;

            GameName = String.Empty;
            GameCreator = String.Empty;
            IsVerified = false;
            Playing = 0;
            GameBitmap = null;

            IsResolving = false;

            Show(null);

            NotifyJoin();
        }

        private static bool IsValidJobId(string? jobId) =>
            !String.IsNullOrWhiteSpace(jobId) && Guid.TryParse(jobId.Trim(), out _);

        private static bool IsProtocolLink(string value) =>
            value.StartsWith("roblox://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("roblox:", StringComparison.OrdinalIgnoreCase);

        private void Join()
        {
            try
            {
                if (_launchQuery is not null)
                {
                    App.Logger.Info($"Joining {_placeId} from a pasted link");

                    GameServers.Launch(_launchQuery);
                }
                else if (_rawCommand is not null)
                {
                    App.Logger.Info("Joining from a pasted link");

                    Process.Start(new ProcessStartInfo(_rawCommand) { UseShellExecute = true });
                }
                else
                {
                    return;
                }

                IsOpen = false;
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to join a game from a pasted link");
                App.Logger.Error(ex);

                Show(Strings.Menu_Overlay_JoinByLink_JoinFailed);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            CancelResolve();

            GC.SuppressFinalize(this);
        }
    }
}
