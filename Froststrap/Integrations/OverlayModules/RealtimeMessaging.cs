using Froststrap.Models.APIs.RealtimeMessaging;
using Froststrap.Models.APIs.RobloxParty.Events;
using Froststrap;
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

        private readonly Uri _userhubUrl = new("wss://realtime-signalr.roblox.com/userhub");

        private ClientWebSocket? _webSocket;
        private SemaphoreSlim? _sendLock;
        private CancellationTokenSource? _cancellation;

        private Task? _receiveTask;
        private Task? _pingTask;

        public event EventHandler<MessageEvent>? PartyChat;
        public event EventHandler<SignalrMessage>? MessageReceived;
        public event EventHandler? Connected;

        public readonly RobloxParty Party;

        public bool IsConnected => _webSocket is not null && _webSocket.State == WebSocketState.Open;

        public RealtimeMessaging()
        {
            Party = new RobloxParty(this);
            MessageReceived += ProcessEvent;
        }

        #region Connection

        public async void ConnectToUserhub()
        {
            if (!App.Settings.Prop.AllowCookieAccess)
            {
                App.Logger.Warn("Cookie access is off, not connecting");
                return;
            }

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

            _receiveTask = ReceiveLoop(_cancellation.Token);
            _pingTask = PingLoop(_cancellation.Token);
        }

        public async Task DisconnectFromUserhub()
        {
            App.Logger.Info("Disconnecting from userhub");

            _cancellation?.Cancel();

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

            _cancellation?.Dispose();
            _webSocket?.Dispose();
            _sendLock?.Dispose();

            _cancellation = null;
            _webSocket = null;
            _sendLock = null;
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectFromUserhub();
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Data processing

        private async Task ReceiveLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && IsConnected)
                {
                    string frame;

                    using (var stream = new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        byte[] buffer = new byte[4096];

                        do
                        {
                            result = await _webSocket!.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                            if (result.MessageType == WebSocketMessageType.Close)
                                return;

                            await stream.WriteAsync(buffer, 0, result.Count, token);
                        }
                        while (!result.EndOfMessage);

                        frame = Encoding.UTF8.GetString(stream.ToArray());
                    }

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
            if (!IsConnected || _sendLock is null)
            {
                App.Logger.Warn("Tried to send after the socket closed");
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(payload + RecordSeparator);

            try
            {
                await _sendLock.WaitAsync();
                await _webSocket!.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to send");
                App.Logger.Error(ex);
            }
            finally
            {
                _sendLock?.Release();
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
                        var message = JsonSerializer.Deserialize<MessageEvent>(payload);

                        if (message is null)
                            throw new JsonException("Deserialised MessageEvent is null");

                        PartyChat?.Invoke(this, message);
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