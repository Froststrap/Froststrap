using DiscordRPC;
using System.Net.Sockets;

namespace Froststrap.Integrations
{
    public class PlayerDiscordRichPresence : IDisposable
    {
        private readonly DiscordRpcClient? _rpcClient;
        private readonly ActivityWatcher _activityWatcher;
        private readonly Queue<Message> _messageQueue = new();
        private readonly bool _isMacOS;

        private DiscordRPC.RichPresence? _currentPresence;
        private DiscordRPC.RichPresence? _originalPresence;

        private readonly FixedSizeList<ThumbnailCacheEntry> _thumbnailCache = new(20);

        private ulong? _smallImgBeingFetched = null;
        private ulong? _largeImgBeingFetched = null;
        private CancellationTokenSource? _fetchThumbnailsToken;

        private bool _visible = true;
        private bool _disposed = false;

        public PlayerDiscordRichPresence(ActivityWatcher activityWatcher)
        {
            const string LOG_IDENT = "PlayerDiscordRichPresence";

            _isMacOS = OperatingSystem.IsMacOS();

            if (_isMacOS)
            {
                App.Logger.WriteLine(LOG_IDENT, "Skipping Discord RPC initialization on macOS");
                _rpcClient = null!;
                _activityWatcher = activityWatcher;
                return;
            }

            _rpcClient = new DiscordRpcClient("363445589247131668");
            _activityWatcher = activityWatcher;

            _activityWatcher.OnGameJoin += (_, _) => Task.Run(() => SetCurrentGame());
            _activityWatcher.OnGameLeave += (_, _) => Task.Run(() => SetCurrentGame());
            _activityWatcher.OnRPCMessage += (_, message) => ProcessRPCMessage(message);

            _rpcClient.OnReady += (_, e) => App.Logger.WriteLine(LOG_IDENT, $"Received ready from user {e.User} ({e.User.ID})");

            _rpcClient.OnPresenceUpdate += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, "Presence updated");

            _rpcClient.OnError += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"An RPC error occurred - {e.Message}");

            _rpcClient.OnConnectionEstablished += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, "Established connection with Discord RPC");

            _rpcClient.OnClose += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"Lost connection to Discord RPC - {e.Reason} ({e.Code})");

            try
            {
                _rpcClient.Initialize();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to init RPC: {ex.Message}");
            }
        }

        public void ProcessRPCMessage(Message message, bool implicitUpdate = true)
        {
            if (_isMacOS || _disposed) return;

            const string LOG_IDENT = "DiscordRichPresence::ProcessRPCMessage";

            if (message.Command != "SetRichPresence" && message.Command != "SetLaunchData")
                return;

            if (_currentPresence is null || _originalPresence is null)
            {
                App.Logger.WriteLine(LOG_IDENT, "Presence is not set, enqueuing message");
                _messageQueue.Enqueue(message);
                return;
            }

            if (message.Command == "SetLaunchData")
            {
                _currentPresence.Buttons = GetButtons();
            }
            else if (message.Command == "SetRichPresence")
            {
                ProcessSetRichPresence(message, implicitUpdate);
            }

            if (implicitUpdate)
                UpdatePresence();
        }

        private void AddToThumbnailCache(ulong id, string? url)
        {
            if (url != null)
                _thumbnailCache.Add(new ThumbnailCacheEntry { Id = id, Url = url });
        }

        private async Task UpdatePresenceIconsAsync(ulong? smallImg, ulong? largeImg, bool implicitUpdate, CancellationToken token)
        {
            if (_isMacOS || _disposed) return;

            Debug.Assert(smallImg != null || largeImg != null);

            if (smallImg != null && largeImg != null)
            {
                string?[] urls = await Thumbnails.GetThumbnailUrlsAsync(
                [
                    new()
                    {
                        TargetId = (ulong)smallImg,
                        Type = ThumbnailType.Asset,
                        Size = "512x512",
                        IsCircular = false
                    },
                    new()
                    {
                        TargetId = (ulong)largeImg,
                        Type = ThumbnailType.Asset,
                        Size = "512x512",
                        IsCircular = false
                    }
                ], token);

                string? smallUrl = urls[0];
                string? largeUrl = urls[1];

                AddToThumbnailCache((ulong)smallImg, smallUrl);
                AddToThumbnailCache((ulong)largeImg, largeUrl);

                if (_currentPresence != null)
                {
                    _currentPresence.Assets.SmallImageKey = smallUrl ?? string.Empty;
                    _currentPresence.Assets.LargeImageKey = largeUrl ?? string.Empty;
                }
            }
            else if (smallImg != null)
            {
                string? url = await Thumbnails.GetThumbnailUrlAsync(new()
                {
                    TargetId = (ulong)smallImg,
                    Type = ThumbnailType.Asset,
                    Size = "512x512",
                    IsCircular = false
                }, token);

                AddToThumbnailCache((ulong)smallImg, url);

                _currentPresence?.Assets.SmallImageKey = url ?? string.Empty;
            }
            else if (largeImg != null)
            {
                string? url = await Thumbnails.GetThumbnailUrlAsync(new()
                {
                    TargetId = (ulong)largeImg,
                    Type = ThumbnailType.Asset,
                    Size = "512x512",
                    IsCircular = false
                }, token);

                AddToThumbnailCache((ulong)largeImg, url);

                _currentPresence?.Assets.LargeImageKey = url ?? string.Empty;
            }

            _smallImgBeingFetched = null;
            _largeImgBeingFetched = null;

            if (implicitUpdate)
                UpdatePresence();
        }

        private void ProcessSetRichPresence(Message message, bool implicitUpdate)
        {
            if (_isMacOS || _disposed) return;

            const string LOG_IDENT = "DiscordRichPresence::ProcessSetRichPresence";
            Models.BloxstrapRPC.RichPresence? presenceData;

            Debug.Assert(_currentPresence is not null);
            Debug.Assert(_originalPresence is not null);

            _fetchThumbnailsToken?.Cancel();
            _fetchThumbnailsToken = null;

            try
            {
                presenceData = message.Data.Deserialize<Models.BloxstrapRPC.RichPresence>();
            }
            catch (Exception)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization threw an exception)");
                return;
            }

            if (presenceData is null)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                return;
            }

            if (presenceData.Details is not null)
            {
                if (presenceData.Details.Length > 128)
                    App.Logger.WriteLine(LOG_IDENT, $"Details cannot be longer than 128 characters");
                else if (presenceData.Details == "<reset>")
                    _currentPresence.Details = _originalPresence.Details;
                else
                    _currentPresence.Details = presenceData.Details;
            }

            if (presenceData.State is not null)
            {
                if (presenceData.State.Length > 128)
                    App.Logger.WriteLine(LOG_IDENT, $"State cannot be longer than 128 characters");
                else if (presenceData.State == "<reset>")
                    _currentPresence.State = _originalPresence.State;
                else
                    _currentPresence.State = presenceData.State;
            }

            if (presenceData.TimestampStart == 0)
                _currentPresence.Timestamps.Start = null;
            else if (presenceData.TimestampStart is not null)
                _currentPresence.Timestamps.StartUnixMilliseconds = presenceData.TimestampStart * 1000;

            if (presenceData.TimestampEnd == 0)
                _currentPresence.Timestamps.End = null;
            else if (presenceData.TimestampEnd is not null)
                _currentPresence.Timestamps.EndUnixMilliseconds = presenceData.TimestampEnd * 1000;

            // set these to start fetching
            ulong? smallImgFetch = null;
            ulong? largeImgFetch = null;

            if (presenceData.SmallImage is not null && !App.Settings.Prop.ShowAccountOnRichPresence)
            {
                if (presenceData.SmallImage.Clear)
                {
                    _currentPresence.Assets.SmallImageKey = _originalPresence.Assets.SmallImageKey;
                    _currentPresence.Assets.SmallImageText = _originalPresence.Assets.SmallImageText;
                    _smallImgBeingFetched = null;
                }
                else if (presenceData.SmallImage.Reset)
                {
                    _currentPresence.Assets.SmallImageText = _originalPresence.Assets.SmallImageText;
                    _currentPresence.Assets.SmallImageKey = _originalPresence.Assets.SmallImageKey;
                    _smallImgBeingFetched = null;
                }
                else
                {
                    if (presenceData.SmallImage.AssetId is not null)
                    {
                        ThumbnailCacheEntry? entry = _thumbnailCache.FirstOrDefault(x => x.Id == presenceData.SmallImage.AssetId);

                        if (entry == null)
                        {
                            smallImgFetch = presenceData.SmallImage.AssetId;
                        }
                        else
                        {
                            _currentPresence.Assets.SmallImageKey = entry.Url ?? string.Empty;
                            _smallImgBeingFetched = null;
                        }
                    }

                    if (presenceData.SmallImage.HoverText is not null)
                        _currentPresence.Assets.SmallImageText = presenceData.SmallImage.HoverText;
                }
            }

            if (presenceData.LargeImage is not null)
            {
                if (presenceData.LargeImage.Clear)
                {
                    _currentPresence.Assets.LargeImageKey = _originalPresence.Assets.LargeImageKey;
                    _currentPresence.Assets.LargeImageText = _originalPresence.Assets.LargeImageText;
                    _largeImgBeingFetched = null;
                }
                else if (presenceData.LargeImage.Reset)
                {
                    _currentPresence.Assets.LargeImageText = _originalPresence.Assets.LargeImageText;
                    _currentPresence.Assets.LargeImageKey = _originalPresence.Assets.LargeImageKey;
                    _largeImgBeingFetched = null;
                }
                else
                {
                    if (presenceData.LargeImage.AssetId is not null)
                    {
                        ThumbnailCacheEntry? entry = _thumbnailCache.FirstOrDefault(x => x.Id == presenceData.LargeImage.AssetId);

                        if (entry == null)
                        {
                            largeImgFetch = presenceData.LargeImage.AssetId;
                        }
                        else
                        {
                            _currentPresence.Assets.LargeImageKey = entry.Url ?? string.Empty;
                            _largeImgBeingFetched = null;
                        }
                    }

                    if (presenceData.LargeImage.HoverText is not null)
                        _currentPresence.Assets.LargeImageText = presenceData.LargeImage.HoverText;
                }
            }

            _smallImgBeingFetched = smallImgFetch;
            _largeImgBeingFetched = largeImgFetch;

            if (_smallImgBeingFetched != null || _largeImgBeingFetched != null)
            {
                _fetchThumbnailsToken = new CancellationTokenSource();
                Task.Run(() => UpdatePresenceIconsAsync(_smallImgBeingFetched, _largeImgBeingFetched, implicitUpdate, _fetchThumbnailsToken.Token));
            }
        }

        public void SetVisibility(bool visible)
        {
            if (_isMacOS || _disposed) return;

            App.Logger.WriteLine("DiscordRichPresence::SetVisibility", $"Setting presence visibility ({visible})");

            _visible = visible;

            if (_visible)
                UpdatePresence();
            else
                _rpcClient?.ClearPresence();
        }

        public async Task<bool> SetCurrentGame()
        {
            if (_isMacOS || _disposed) return false;

            const string LOG_IDENT = "DiscordRichPresence::SetCurrentGame";

            if (!_activityWatcher.InGame)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not in game, clearing presence");
                _currentPresence = _originalPresence = null;
                _messageQueue.Clear();
                UpdatePresence();
                return true;
            }

            var activity = _activityWatcher.Data;
            App.Logger.WriteLine(LOG_IDENT, $"Setting presence for Place ID {activity.PlaceId}");

            var timeStarted = activity.RootActivity?.TimeJoined ?? activity.TimeJoined;

            if (activity.UniverseDetails is null)
            {
                try { await UniverseDetails.FetchSingle(activity.UniverseId); }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                    return false;
                }
                activity.UniverseDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
            }

            var universeDetails = activity.UniverseDetails!;

            string icon = universeDetails.Thumbnail.ImageUrl!;
            string smallImage = "roblox";
            string smallImageText = "Roblox";

            if (App.Settings.Prop.ShowAccountOnRichPresence)
            {
                var userDetails = await UserDetails.Fetch(activity.UserId);
                smallImage = userDetails.Thumbnail.ImageUrl!;
                smallImageText = $"Playing on {userDetails.Data.DisplayName} (@{userDetails.Data.Name})";
            }

            if (!_activityWatcher.InGame || activity.PlaceId != _activityWatcher.Data.PlaceId)
                return false;

            string status = activity.ServerType switch
            {
                ServerType.Private => "In a Private server",
                ServerType.Reserved => "In a Reserved server",
                _ => $"by {universeDetails.Data.Creator.Name}" + (universeDetails.Data.Creator.HasVerifiedBadge ? " ☑️" : ""),
            };

            string universeName = universeDetails.Data.Name;
            if (universeName.Length < 2) universeName = $"{universeName}\x2800\x2800\x2800";

            _currentPresence = new DiscordRPC.RichPresence
            {
                Details = universeName,
                StatusDisplay = App.Settings.Prop.EnableCustomStatusDisplay ? StatusDisplayType.Details : (StatusDisplayType)0,
                State = status,
                Timestamps = new Timestamps { Start = timeStarted.ToUniversalTime() },
                Buttons = GetButtons(),
                Assets = new Assets
                {
                    LargeImageKey = icon,
                    LargeImageText = universeDetails.Data.Name,
                    SmallImageKey = smallImage,
                    SmallImageText = smallImageText
                }
            };

            _originalPresence = _currentPresence.Clone();

            if (_messageQueue.TryDequeue(out var queuedMessage))
            {
                App.Logger.WriteLine(LOG_IDENT, "Processing queued messages");
                ProcessRPCMessage(queuedMessage, false);
            }

            UpdatePresence();
            return true;
        }

        public Button[] GetButtons()
        {
            List<Button> buttons = [];

            var data = _activityWatcher.Data;

            if (!App.Settings.Prop.HideRPCButtons)
            {
                bool show = false;

                if (data.ServerType == ServerType.Public)
                    show = true;
                else if (data.ServerType == ServerType.Reserved && !string.IsNullOrEmpty(data.RPCLaunchData))
                    show = true;

                if (show)
                {
                    buttons.Add(new Button
                    {
                        Label = "Join server",
                        Url = data.GetInviteDeeplink()
                    });
                }
            }

            buttons.Add(new Button
            {
                Label = "See game page",
                Url = $"https://www.roblox.com/games/{data.PlaceId}"
            });

            return [.. buttons];
        }

        public void UpdatePresence()
        {
            if (_isMacOS || _disposed || _rpcClient == null) return;

            const string LOG_IDENT = "DiscordRichPresence::UpdatePresence";

            if (!_rpcClient.IsInitialized)
                return;

            try
            {
                if (_visible)
                {
                    if (_currentPresence != null)
                    {
                        _currentPresence.Assets ??= new Assets();
                        App.Logger.WriteLine(LOG_IDENT, "Updating presence");
                        _rpcClient.SetPresence(_currentPresence);
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Clearing presence (no current presence)");
                        _rpcClient.ClearPresence();
                    }
                }
                else
                {
                    _rpcClient.ClearPresence();
                }
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Socket interrupted (Operation Canceled). This is expected on macOS.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            const string LOG_IDENT = "DiscordRichPresence::Dispose";
            App.Logger.WriteLine(LOG_IDENT, "Cleaning up Discord RPC and Presence");

            if (_rpcClient != null)
            {
                try
                {
                    _fetchThumbnailsToken?.Cancel();
                    _fetchThumbnailsToken?.Dispose();

                    if (_rpcClient.IsInitialized)
                    {
                        try { _rpcClient.ClearPresence(); } catch (IOException) { }
                    }

                    _rpcClient.Dispose();
                }
                catch (IOException ex) when (ex.InnerException is SocketException) { }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }

            GC.SuppressFinalize(this);
        }
    }
}