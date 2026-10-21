// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text.Json;

namespace Froststrap.Integrations.Discord
{
    internal enum StatusDisplayType
    {
        Name = 0,
        State = 1,
        Details = 2
    }

    internal sealed class Button
    {
        public string? Label { get; set; }
        public string? Url { get; set; }
    }

    internal sealed class Assets
    {
        public string? LargeImageKey { get; set; }
        public string? LargeImageText { get; set; }
        public string? SmallImageKey { get; set; }
        public string? SmallImageText { get; set; }

        public Assets Clone() => new()
        {
            LargeImageKey = LargeImageKey,
            LargeImageText = LargeImageText,
            SmallImageKey = SmallImageKey,
            SmallImageText = SmallImageText
        };
    }

    internal sealed class Timestamps
    {
        private DateTime? _start;
        private DateTime? _end;

        public DateTime? Start
        {
            get => _start;
            set
            {
                _start = value;
                StartUnixMilliseconds = value.HasValue
                    ? (ulong)new DateTimeOffset(value.Value.ToUniversalTime()).ToUnixTimeMilliseconds()
                    : null;
            }
        }

        public DateTime? End
        {
            get => _end;
            set
            {
                _end = value;
                EndUnixMilliseconds = value.HasValue
                    ? (ulong)new DateTimeOffset(value.Value.ToUniversalTime()).ToUnixTimeMilliseconds()
                    : null;
            }
        }

        public ulong? StartUnixMilliseconds { get; set; }
        public ulong? EndUnixMilliseconds { get; set; }

        public Timestamps Clone() => new()
        {
            Start = _start,
            End = _end,
            StartUnixMilliseconds = StartUnixMilliseconds,
            EndUnixMilliseconds = EndUnixMilliseconds
        };
    }

    internal sealed class DiscordActivity
    {
        public string? Details { get; set; }
        public string? State { get; set; }
        public StatusDisplayType StatusDisplay { get; set; }
        public Timestamps Timestamps { get; set; } = new();
        public Assets Assets { get; set; } = new();
        public Button[]? Buttons { get; set; }

        public DiscordActivity Clone() => new()
        {
            Details = Details,
            State = State,
            StatusDisplay = StatusDisplay,
            Timestamps = Timestamps.Clone(),
            Assets = Assets.Clone(),
            Buttons = Buttons?.Select(button => new Button { Label = button.Label, Url = button.Url }).ToArray()
        };
    }

    internal sealed class User
    {
        public string Username { get; set; } = "";
        public string ID { get; set; } = "";

        public override string ToString() => Username;
    }

    internal sealed class ReadyMessage
    {
        public User User { get; set; } = new();
    }

    internal sealed class ErrorMessage
    {
        public string Message { get; set; } = "";
        public int Code { get; set; }
    }

    internal sealed class CloseMessage
    {
        public string Reason { get; set; } = "";
        public int Code { get; set; }
    }

    internal sealed class PresenceUpdateMessage
    {
    }

    internal sealed class ConnectionEstablishedMessage
    {
    }

    internal sealed class DiscordRpcClient : IDisposable
    {
        private const int PipeCount = 10;
        private const int MaxFrameSize = 1024 * 1024;
        private const int OpcodeHandshake = 0;
        private const int OpcodeFrame = 1;
        private const int OpcodeClose = 2;
        private const int OpcodePing = 3;
        private const int OpcodePong = 4;

        private readonly string _applicationId;
        private readonly Thread _worker;
        private readonly Thread _presenceWriter;
        private readonly object _writeLock = new();
        private readonly object _presenceLock = new();

        private Stream? _stream;
        private DiscordActivity? _presence;
        private byte[]? _pendingCommand;
        private string? _lastActivitySignature;
        private volatile bool _connected;
        private volatile bool _disposed;

        public DiscordRpcClient(string applicationId)
        {
            _applicationId = applicationId;
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "Discord RPC"
            };
            _presenceWriter = new Thread(PresenceWriterLoop)
            {
                IsBackground = true,
                Name = "Discord RPC Presence"
            };
        }

        public bool IsInitialized { get; private set; }
        public bool SkipIdenticalPresence { get; set; } = true;
        public bool ShutdownOnly { get; set; } = true;

        public event EventHandler<ReadyMessage>? OnReady;
        public event EventHandler<ErrorMessage>? OnError;
        public event EventHandler<PresenceUpdateMessage>? OnPresenceUpdate;
        public event EventHandler<ConnectionEstablishedMessage>? OnConnectionEstablished;
        public event EventHandler<CloseMessage>? OnClose;

        public void Initialize()
        {
            if (IsInitialized)
                return;

            IsInitialized = true;
            _presenceWriter.Start();
            _worker.Start();
        }

        public void SetPresence(DiscordActivity presence)
        {
            if (_disposed)
                return;

            lock (_presenceLock)
            {
                _presence = presence;

                if (!_connected)
                    return;

                string signature = SerializeActivity(presence);

                if (SkipIdenticalPresence && signature == _lastActivitySignature)
                    return;

                _lastActivitySignature = signature;
                _pendingCommand = BuildCommand(presence);
                Monitor.Pulse(_presenceLock);
            }
        }

        public void ClearPresence()
        {
            if (_disposed)
                return;

            lock (_presenceLock)
            {
                _presence = null;
                _lastActivitySignature = null;

                if (!_connected)
                    return;

                _pendingCommand = BuildCommand(null);
                Monitor.Pulse(_presenceLock);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            lock (_presenceLock)
                Monitor.PulseAll(_presenceLock);

            try { _stream?.Dispose(); }
            catch
            {
            }

            _stream = null;

            GC.SuppressFinalize(this);
        }

        private void WorkerLoop()
        {
            long backoff = 500;

            while (!_disposed)
            {
                Stream? stream = null;

                try { stream = TryConnect(); }
                catch { }

                if (stream == null)
                {
                    if (!WaitBackoff(backoff))
                        return;

                    backoff = Math.Min(backoff * 2, 60000);
                    continue;
                }

                lock (_presenceLock)
                {
                    _stream = stream;
                    _connected = true;
                    _lastActivitySignature = null;
                }

                backoff = 500;

                try
                {
                    SendHandshake();
                    OnConnectionEstablished?.Invoke(this, new ConnectionEstablishedMessage());

                    lock (_presenceLock)
                    {
                        _connected = true;

                        if (_presence != null)
                        {
                            _lastActivitySignature = SerializeActivity(_presence);
                            _pendingCommand = BuildCommand(_presence);
                        }
                    }

                    lock (_presenceLock)
                        Monitor.Pulse(_presenceLock);

                    ReadLoop(stream);
                }
                catch
                {
                }
                finally
                {
                    lock (_presenceLock)
                    {
                        _connected = false;
                        _stream = null;
                        _pendingCommand = null;
                    }

                    try { stream.Dispose(); }
                    catch { }

                    if (!_disposed)
                        OnClose?.Invoke(this, new CloseMessage { Reason = "Connection closed", Code = 0 });
                }

                if (!WaitBackoff(backoff))
                    return;
            }
        }

        private void PresenceWriterLoop()
        {
            while (true)
            {
                byte[]? command;

                lock (_presenceLock)
                {
                    while (!_disposed && _pendingCommand == null)
                        Monitor.Wait(_presenceLock);

                    if (_disposed)
                        return;

                    command = _pendingCommand;
                    _pendingCommand = null;
                }

                if (command != null)
                    SendFrame(command);
            }
        }

        private bool WaitBackoff(long milliseconds)
        {
            for (long waited = 0; waited < milliseconds; waited += 100)
            {
                if (_disposed)
                    return false;

                Thread.Sleep(100);
            }

            return !_disposed;
        }

        private void ReadLoop(Stream stream)
        {
            byte[] header = new byte[8];

            while (!_disposed)
            {
                stream.ReadExactly(header, 0, header.Length);

                uint opcode = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4));
                uint length = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4));

                if (length > MaxFrameSize)
                    return;

                byte[] data = new byte[length];

                if (length > 0)
                    stream.ReadExactly(data, 0, (int)length);

                if (opcode == OpcodeClose)
                    return;

                if (opcode == OpcodePing)
                {
                    SendRawFrame(OpcodePong, data);
                    continue;
                }

                if (opcode == OpcodeFrame)
                    HandleFrame(data);
            }
        }

        private void HandleFrame(byte[] data)
        {
            try
            {
                using var document = JsonDocument.Parse(data);
                var root = document.RootElement;

                if (root.TryGetProperty("cmd", out var cmd) && cmd.ValueKind == JsonValueKind.String)
                {
                    if (cmd.GetString() == "SET_ACTIVITY")
                        OnPresenceUpdate?.Invoke(this, new PresenceUpdateMessage());

                    return;
                }

                if (root.TryGetProperty("evt", out var evt) && evt.ValueKind == JsonValueKind.String)
                {
                    switch (evt.GetString())
                    {
                        case "READY":
                            OnReady?.Invoke(this, new ReadyMessage { User = ParseUser(root) });
                            break;
                        case "ERROR":
                            OnError?.Invoke(this, ParseError(root));
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to read Discord RPC frame: {ex.Message}");
            }
        }

        private static User ParseUser(JsonElement root)
        {
            var user = new User();

            if (root.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("user", out var element) &&
                element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("username", out var username) && username.ValueKind == JsonValueKind.String)
                    user.Username = username.GetString() ?? "";

                if (element.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    user.ID = id.GetString() ?? "";
            }

            return user;
        }

        private static ErrorMessage ParseError(JsonElement root)
        {
            var error = new ErrorMessage();

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                if (data.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    error.Message = message.GetString() ?? "";

                if (data.TryGetProperty("code", out var code) && code.TryGetInt32(out int number))
                    error.Code = number;
            }

            return error;
        }

        private void SendHandshake()
        {
            using var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteNumber("v", 1);
                writer.WriteString("client_id", _applicationId);
                writer.WriteEndObject();
            }

            SendRawFrame(OpcodeHandshake, stream.ToArray());
        }

        private void SendFrame(byte[] payload) => SendRawFrame(OpcodeFrame, payload);

        private void SendClose() => SendRawFrame(OpcodeClose, []);

        private void SendRawFrame(int opcode, byte[] data)
        {
            var stream = _stream;

            if (stream == null)
                return;

            try
            {
                lock (_writeLock)
                {
                    Span<byte> header = stackalloc byte[8];
                    BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)opcode);
                    BinaryPrimitives.WriteUInt32LittleEndian(header[4..], (uint)data.Length);

                    stream.Write(header);
                    stream.Write(data);
                    stream.Flush();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to send Discord RPC frame: {ex.Message}");
            }
        }

        private static byte[] BuildCommand(DiscordActivity? presence)
        {
            using var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("cmd", "SET_ACTIVITY");
                writer.WriteString("nonce", Guid.NewGuid().ToString());
                writer.WriteStartObject("args");
                writer.WriteNumber("pid", Environment.ProcessId);

                if (presence != null)
                {
                    writer.WriteStartObject("activity");
                    WriteActivity(writer, presence);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            return stream.ToArray();
        }

        private static string SerializeActivity(DiscordActivity presence)
        {
            using var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                WriteActivity(writer, presence);
                writer.WriteEndObject();
            }

            return Convert.ToBase64String(stream.ToArray());
        }

        private static void WriteActivity(Utf8JsonWriter writer, DiscordActivity presence)
        {
            if (!string.IsNullOrEmpty(presence.Details))
                writer.WriteString("details", presence.Details);

            if (!string.IsNullOrEmpty(presence.State))
                writer.WriteString("state", presence.State);

            writer.WriteNumber("status_display_type", (int)presence.StatusDisplay);

            ulong? start = presence.Timestamps.StartUnixMilliseconds;
            ulong? end = presence.Timestamps.EndUnixMilliseconds;

            if (start != null || end != null)
            {
                writer.WriteStartObject("timestamps");

                if (start != null)
                    writer.WriteNumber("start", start.Value);

                if (end != null)
                    writer.WriteNumber("end", end.Value);

                writer.WriteEndObject();
            }

            var assets = presence.Assets;

            if (assets != null &&
                (!string.IsNullOrEmpty(assets.LargeImageKey) ||
                 !string.IsNullOrEmpty(assets.LargeImageText) ||
                 !string.IsNullOrEmpty(assets.SmallImageKey) ||
                 !string.IsNullOrEmpty(assets.SmallImageText)))
            {
                writer.WriteStartObject("assets");

                if (!string.IsNullOrEmpty(assets.LargeImageKey))
                    writer.WriteString("large_image", assets.LargeImageKey);

                if (!string.IsNullOrEmpty(assets.LargeImageText))
                    writer.WriteString("large_text", assets.LargeImageText);

                if (!string.IsNullOrEmpty(assets.SmallImageKey))
                    writer.WriteString("small_image", assets.SmallImageKey);

                if (!string.IsNullOrEmpty(assets.SmallImageText))
                    writer.WriteString("small_text", assets.SmallImageText);

                writer.WriteEndObject();
            }

            if (presence.Buttons is { Length: > 0 })
            {
                writer.WriteStartArray("buttons");

                foreach (var button in presence.Buttons.Take(2))
                {
                    if (string.IsNullOrEmpty(button.Label) || string.IsNullOrEmpty(button.Url))
                        continue;

                    writer.WriteStartObject();
                    writer.WriteString("label", button.Label);
                    writer.WriteString("url", button.Url);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }
        }

        private static Stream? TryConnect()
        {
            if (OperatingSystem.IsWindows())
            {
                for (int i = 0; i < PipeCount; i++)
                {
                    NamedPipeClientStream pipe = new(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.None);

                    try
                    {
                        pipe.Connect(50);
                        return pipe;
                    }
                    catch
                    {
                        pipe.Dispose();
                    }
                }

                return null;
            }

            foreach (string path in ResolveUnixPaths())
            {
                Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

                try
                {
                    socket.Connect(new UnixDomainSocketEndPoint(path));
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                }
            }

            return null;
        }

        private static IEnumerable<string> ResolveUnixPaths()
        {
            foreach (string directory in ResolveUnixDirectories())
            {
                for (int i = 0; i < PipeCount; i++)
                    yield return Path.Combine(directory, $"discord-ipc-{i}");
            }
        }

        private static IEnumerable<string> ResolveUnixDirectories()
        {
            foreach (string variable in new[] { "XDG_RUNTIME_DIR", "TMPDIR", "TMP", "TEMP" })
            {
                string? value = Environment.GetEnvironmentVariable(variable);

                if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
                    yield return value;
            }

            if (OperatingSystem.IsMacOS())
            {
                foreach (string directory in MacGuiTempDirectories())
                    yield return directory;
            }
        }

        private static IEnumerable<string> MacGuiTempDirectories()
        {
            foreach (string root in new[] { "/private/var/folders", "/var/folders" })
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (string level1 in SafeEnumerateDirectories(root))
                {
                    foreach (string level2 in SafeEnumerateDirectories(level1))
                    {
                        string temp = Path.Combine(level2, "T");

                        if (!Directory.Exists(temp))
                            continue;

                        bool hasPipe;
                        try
                        {
                            hasPipe = Directory.EnumerateFileSystemEntries(temp, "discord-ipc-*").Any();
                        }
                        catch
                        {
                            hasPipe = false;
                        }

                        if (hasPipe)
                            yield return temp;
                    }
                }
            }
        }

        private static IEnumerable<string> SafeEnumerateDirectories(string path)
        {
            string[] directories;

            try
            {
                directories = Directory.EnumerateDirectories(path).ToArray();
            }
            catch
            {
                yield break;
            }

            foreach (string directory in directories)
                yield return directory;
        }
    }
}
