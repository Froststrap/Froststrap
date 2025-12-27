using DiscordRPC;

namespace Bloxstrap.Integrations
{
    public class StudioDiscordRichPresence : IDisposable
    {
        private readonly DiscordRpcClient _rpcClient = new("1454451301130960896");
        private readonly ActivityWatcher _activityWatcher;
        private readonly Queue<Message> _messageQueue = new();

        private DiscordRPC.RichPresence? _currentPresence;
        private DiscordRPC.RichPresence? _originalPresence;

        private bool _visible = true;

        public StudioDiscordRichPresence(ActivityWatcher activityWatcher)
        {
            const string LOG_IDENT = "StudioDiscordRichPresence";

            _activityWatcher = activityWatcher;
            _activityWatcher.OnRPCMessage += (_, message) => ProcessRPCMessage(message);

            _rpcClient.OnReady += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"Received ready from user {e.User} ({e.User.ID})");

            _rpcClient.OnConnectionEstablished += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, "Established connection with Discord RPC");

            _rpcClient.OnClose += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"Lost connection to Discord RPC - {e.Reason} ({e.Code})");

            _rpcClient.OnError += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"An RPC error occurred - {e.Message}");

            _rpcClient.Initialize();

            InitializeStudioPresence();
        }

        public void ProcessRPCMessage(Message message, bool implicitUpdate = true)
        {
            const string LOG_IDENT = "StudioDiscordRichPresence::ProcessRPCMessage";

            if (message.Command != "SetRichPresence")
                return;

            if (_currentPresence is null || _originalPresence is null)
            {
                App.Logger.WriteLine(LOG_IDENT, "Presence is not set, initializing Studio presence");
                InitializeStudioPresence();
            }

            ProcessStudioRichPresence(message, implicitUpdate);

            if (implicitUpdate)
                UpdatePresence();
        }

        private void InitializeStudioPresence()
        {
            App.Logger.WriteLine("StudioDiscordRichPresence::InitializeStudioPresence", "Initializing Studio presence");

            _currentPresence = new DiscordRPC.RichPresence
            {
                Timestamps = new Timestamps { Start = DateTime.UtcNow },
                Assets = new Assets
                {
                    LargeImageKey = "roblox_studio",
                    LargeImageText = "Roblox Studio",
                },
            };

            _originalPresence = _currentPresence.Clone();

            while (_messageQueue.Any())
            {
                ProcessRPCMessage(_messageQueue.Dequeue(), false);
            }

            UpdatePresence();
        }

        private void ProcessStudioRichPresence(Message message, bool implicitUpdate)
        {
            const string LOG_IDENT = "StudioDiscordRichPresence::ProcessStudioRichPresence";
            StudioRichPresence? presenceData;

            Debug.Assert(_currentPresence is not null);
            Debug.Assert(_originalPresence is not null);

            try
            {
                presenceData = message.Data.Deserialize<StudioRichPresence>();
            }
            catch (Exception)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to parse studio message!");
                return;
            }

            if (presenceData is null)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to parse studio message!");
                return;
            }

            DateTime? currentTimestamp = _currentPresence.Timestamps.Start;

            if (!string.IsNullOrEmpty(presenceData.Details))
                _currentPresence.Details = presenceData.Details;

            if (!string.IsNullOrEmpty(presenceData.State))
                _currentPresence.State = presenceData.State;

            _currentPresence.Timestamps.Start = currentTimestamp;

            string largeImageKey = "roblox_studio";
            string largeImageText = "Roblox Studio";

            if (!string.IsNullOrEmpty(presenceData.ScriptType))
            {
                switch (presenceData.ScriptType.ToLower())
                {
                    case "server":
                        largeImageKey = "studio_server";
                        largeImageText = "Editing Server Script";
                        break;
                    case "client":
                        largeImageKey = "studio_client";
                        largeImageText = "Editing Client Script";
                        break;
                    case "server_module":
                    case "client_module":
                    case "module":
                        largeImageKey = "studio_module";
                        largeImageText = "Editing Module Script";
                        break;
                    case "developing":
                        largeImageKey = "roblox_studio";
                        largeImageText = "Roblox Studio";
                        break;
                }
            }

            string smallImageKey = "froststrap";
            string smallImageText = $"Froststrap {App.Version}";

            if (presenceData.Testing)
            {
                smallImageKey = "testing";
                smallImageText = "Currently Testing";
            }

            _currentPresence.Assets = new Assets
            {
                LargeImageKey = largeImageKey,
                LargeImageText = largeImageText,
                SmallImageKey = smallImageKey,
                SmallImageText = smallImageText
            };

            _originalPresence = _currentPresence.Clone();

            if (implicitUpdate)
                UpdatePresence();
        }

        public void SetVisibility(bool visible)
        {
            App.Logger.WriteLine("StudioDiscordRichPresence::SetVisibility", $"Setting presence visibility ({visible})");

            _visible = visible;

            if (_visible)
                UpdatePresence();
            else
                _rpcClient.ClearPresence();
        }

        public void UpdatePresence()
        {
            const string LOG_IDENT = "StudioDiscordRichPresence::UpdatePresence";

            if (_currentPresence is null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Presence is empty, clearing");
                _rpcClient.ClearPresence();
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Updating presence");

            if (_visible)
                _rpcClient.SetPresence(_currentPresence);
        }

        public void Dispose()
        {
            App.Logger.WriteLine("StudioDiscordRichPresence::Dispose","Cleaning up Discord RPC and Presence");
            _rpcClient.ClearPresence();
            _rpcClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}