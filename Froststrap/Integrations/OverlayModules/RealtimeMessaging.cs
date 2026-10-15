using Froststrap.Models.APIs.RealtimeMessaging;
using Froststrap.Models.APIs.RobloxParty.Events;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Froststrap.Integrations.OverlayModules
{
    internal class RealtimeMessaging : IAsyncDisposable
    {
        private const string RecordSeparator = "\u001e";
        private const int PingType = 6;
        private const int PingIntervalMs = 10000;
        private const int ReceiveBufferSize = 4096;

        private readonly Uri _userhubUrl = new("wss://realtime-signalr.roblox.com/userhub");

        private readonly SemaphoreSlim _connectGate = new(1, 1);

        private ClientWebSocket? _webSocket;
        private SemaphoreSlim? _sendLock;
        private CancellationTokenSource? _cancellation;

        private Task? _receiveTask;
        private Task? _pingTask;
        private bool _disposed;
        private int _connectedState;

        public event EventHandler<MessageEvent>? PartyChat;
        public event EventHandler<SignalrMessage>? MessageReceived;
        public event EventHandler<IReadOnlyList<PresenceNotification>>? PresenceBulk;
        public event EventHandler<FriendshipNotification>? FriendshipChanged;
        public event EventHandler<ChannelMarkedRead>? ChannelRead;
        public event EventHandler<ChannelMetadataUpdate>? ChannelMetadataChanged;
        public event EventHandler? Connected;

        public static event EventHandler<FriendshipNotification>? AnyFriendshipChanged;
        public static event EventHandler<IReadOnlyList<PresenceNotification>>? AnyPresenceBulk;

        private static int _connectedInstances;

        public readonly RobloxParty Party;

        public bool IsConnected => _webSocket is not null && _webSocket.State == WebSocketState.Open;

        public static bool UserhubConnected => Volatile.Read(ref _connectedInstances) > 0;

        private void MarkConnected()
        {
            if (Interlocked.Exchange(ref _connectedState, 1) == 0)
                Interlocked.Increment(ref _connectedInstances);
        }

        private void MarkDisconnected()
        {
            if (Interlocked.Exchange(ref _connectedState, 0) == 1)
                Interlocked.Decrement(ref _connectedInstances);
        }

        public RealtimeMessaging()
        {
            Party = new RobloxParty(this);
            MessageReceived += ProcessEvent;

            App.Cookies.WatchAccount(this, static m => m.OnAccountChanged());
        }

        private void OnAccountChanged()
        {
            if (_disposed)
                return;

            if (App.Cookies.IsAuthenticated)
                _ = ConnectToUserhubAsync();
            else
                _ = DisconnectFromUserhub();
        }

        #region Connection

        public void ConnectToUserhub() => _ = ConnectToUserhubAsync();

        public async Task ConnectToUserhubAsync()
        {
            if (_disposed)
                return;

            if (!App.Settings.Prop.AllowCookieAccess)
            {
                App.Logger.Warn("Cookie access is off, not connecting");
                return;
            }

            await _connectGate.WaitAsync();

            try
            {
                if (_disposed)
                    return;

                if (_webSocket is not null)
                    await DisconnectFromUserhub();

                _sendLock = new SemaphoreSlim(1, 1);
                _webSocket = new ClientWebSocket();
                _cancellation = new CancellationTokenSource();

                try
                {
                    App.Cookies.AuthWebsocket(_webSocket);

                    App.Logger.Info("Connecting to userhub");

                    await _webSocket.ConnectAsync(_userhubUrl, _cancellation.Token);

                    App.Logger.Info("Connected to userhub");

                    MarkConnected();
                }
                catch (Exception ex)
                {
                    App.Logger.Error("Unable to connect to userhub");
                    App.Logger.Error(ex);

                    await DisconnectFromUserhub();
                    return;
                }

                Connected?.Invoke(this, EventArgs.Empty);

                await SendHandshake();

                _receiveTask = ReceiveLoop(_webSocket, _cancellation.Token);
                _pingTask = PingLoop(_cancellation.Token);
            }
            finally
            {
                try { _connectGate.Release(); }
                catch (ObjectDisposedException) { }
            }
        }

        public async Task DisconnectFromUserhub()
        {
            App.Logger.Info("Disconnecting from userhub");

#pragma warning disable CA1849
            _cancellation?.Cancel();
#pragma warning restore CA1849

            foreach (Task? task in new[] { _receiveTask, _pingTask })
            {
                if (task is null)
                    continue;

                try { await task; }
                catch (Exception) { }
            }

            _receiveTask = null;
            _pingTask = null;

            if (IsConnected)
            {
                try { await _webSocket!.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); }
                catch (Exception) { }
            }

            MarkDisconnected();

            _cancellation?.Dispose();
            _webSocket?.Dispose();
            _sendLock?.Dispose();

            _cancellation = null;
            _webSocket = null;
            _sendLock = null;
        }

        public async ValueTask DisposeAsync()
        {
            _disposed = true;

            await DisconnectFromUserhub();

            _connectGate.Dispose();

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Data processing

        private async Task ReceiveLoop(ClientWebSocket socket, CancellationToken token)
        {
            byte[] buffer = new byte[ReceiveBufferSize];

            using var stream = new MemoryStream();

            try
            {
                while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    stream.SetLength(0);

                    ValueWebSocketReceiveResult result;

                    do
                    {
                        result = await socket.ReceiveAsync(buffer.AsMemory(), token);

                        if (result.MessageType == WebSocketMessageType.Close)
                            return;

                        await stream.WriteAsync(buffer.AsMemory(0, result.Count), token);
                    }
                    while (!result.EndOfMessage);

                    string frame = Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);

                    foreach (string record in frame.Split(RecordSeparator, StringSplitOptions.RemoveEmptyEntries))
                        HandleRecord(record.TrimEnd('\0'));
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex)
            {
                App.Logger.Error("Connection dropped");
                App.Logger.Error(ex);
            }
            finally
            {
                MarkDisconnected();
            }
        }

        private void HandleRecord(string record)
        {
            if (String.IsNullOrWhiteSpace(record))
                return;

            try
            {
                var message = JsonSerializer.Deserialize<SignalrMessage>(record);

                if (message is null || message.Type is null || message.Type == PingType)
                    return;

                if (message.Target == "subscriptionStatus")
                    return;

                if (message.Target is null || message.Arguments is null || message.Arguments.Length < 2)
                    return;

                MessageReceived?.Invoke(this, message);
            }
            catch (JsonException ex)
            {
                App.Logger.Error($"Failed to deserialize record\nRaw record:\n{record}");
                App.Logger.Error(ex);
            }
        }

        private async Task PingLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && IsConnected)
                {
                    await SendPing();
                    await Task.Delay(PingIntervalMs, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task SendHandshake() => await SafeSend("{\"protocol\":\"json\",\"version\":1}");

        private async Task SendPing() => await SafeSend($"{{\"type\":{PingType}}}");

        private async Task SafeSend(string payload)
        {
            ClientWebSocket? socket = _webSocket;
            SemaphoreSlim? gate = _sendLock;

            if (socket is null || gate is null || socket.State != WebSocketState.Open)
            {
                App.Logger.Warn("Tried to send after the socket closed");
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(payload + RecordSeparator);

            bool entered = false;

            try
            {
                await gate.WaitAsync();
                entered = true;

                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to send");
                App.Logger.Error(ex);
            }
            finally
            {
                if (entered)
                {
                    try { gate.Release(); }
                    catch (ObjectDisposedException) { }
                }
            }
        }

        private void ProcessEvent(object? sender, SignalrMessage e)
        {
            string target = e.Arguments![0];
            string payload = e.Arguments![1];

            try
            {
                switch (target)
                {
                    case "CommunicationChannels":
                    {
                        using var doc = JsonDocument.Parse(payload);
                        JsonElement root = doc.RootElement;

                        string? type = root.TryGetProperty("Type", out JsonElement t)
                            ? t.GetString()
                            : null;

                        if (String.Equals(type, "ChannelMarkedRead", StringComparison.Ordinal))
                        {
                            var read = root.Deserialize<ChannelMarkedRead>();

                            if (read is not null && !String.IsNullOrEmpty(read.ChannelId))
                                ChannelRead?.Invoke(this, read);
                        }
                        else if (String.Equals(type, "ChannelMetadataUpdated", StringComparison.Ordinal))
                        {
                            var update = root.Deserialize<ChannelMetadataUpdate>();

                            if (update is not null && !String.IsNullOrEmpty(update.ChannelId))
                                ChannelMetadataChanged?.Invoke(this, update);
                        }
                        else
                        {
                            var message = root.Deserialize<MessageEvent>()
                                ?? throw new JsonException("Deserialised MessageEvent is null");

                            PartyChat?.Invoke(this, message);
                        }

                        break;
                    }

                    case "PresenceBulkNotifications":
                        var notifications = JsonSerializer.Deserialize<List<PresenceNotification>>(payload);

                        if (notifications is { Count: > 0 })
                        {
                            PresenceBulk?.Invoke(this, notifications);
                            AnyPresenceBulk?.Invoke(this, notifications);
                        }
                        break;

                    case "FriendshipNotifications":
                        var friendship = JsonSerializer.Deserialize<FriendshipNotification>(payload);

                        if (friendship is not null && friendship.EventArgs is not null)
                        {
                            FriendshipChanged?.Invoke(this, friendship);
                            AnyFriendshipChanged?.Invoke(this, friendship);
                        }
                        break;

                    default:
                        App.Logger.Info($"Unhandled message target: {target}");
                        break;
                }
            }
            catch (JsonException ex)
            {
                App.Logger.Error($"Failed to deserialize a '{target}' payload");
                App.Logger.Error(ex);
            }
        }

        #endregion
    }
}