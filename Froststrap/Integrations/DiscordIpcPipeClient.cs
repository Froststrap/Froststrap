using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using DiscordRPC.IO;
using DiscordRPC.Logging;

namespace Froststrap.Integrations
{
    /// <summary>
    /// A Discord IPC pipe client that reliably connects to Discord on macOS.
    /// <para>
    /// The library default (<see cref="ManagedNamedPipeClient"/>) only looks for the
    /// <c>discord-ipc-N</c> socket inside the directories supplied by the
    /// <c>XDG_RUNTIME_DIR</c>/<c>TMPDIR</c>/<c>TMP</c>/<c>TEMP</c> environment variables.
    /// When a GUI app is launched from a terminal or a dev shell the current
    /// <c>TMPDIR</c> often differs from the GUI session temp directory where Discord
    /// actually publishes its socket (<c>/var/folders/.../T</c> on macOS), so the
    /// stock client never finds it. This client scans the macOS GUI temp
    /// directories as a fallback and talks straight over a raw Unix domain socket.
    /// </para>
    /// </summary>
    internal sealed class DiscordIpcPipeClient : INamedPipeClient
    {
        public const string PipePrefix = "discord-ipc-";
        public const int MaxPipes = 10;
        public const int MaxFrameSize = 1024 * 1024;

        private readonly ConcurrentQueue<PipeFrame> _frames = new();
        private readonly object _writeLock = new();
        private Socket? _socket;
        private volatile bool _closed = true;
        private volatile bool _disposed;

        public ILogger Logger { get; set; } = new NullLogger();

        public bool IsConnected => _socket != null && !_closed && !_disposed;

        public int ConnectedPipe { get; private set; } = -1;

        /// <summary>
        /// Returns a custom pipe client on macOS (where the default transport has
        /// trouble finding the Discord socket) and null on every other OS so the
        /// library keeps using its own <see cref="ManagedNamedPipeClient"/>.
        /// </summary>
        public static INamedPipeClient? Create()
            => OperatingSystem.IsMacOS() ? new DiscordIpcPipeClient() : null;

        public bool Connect(int pipe)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            Close();

            foreach (string path in ResolvePipePaths(pipe))
            {
                if (TryConnect(path, out Socket? socket))
                {
                    _socket = socket;
                    _closed = false;
                    ConnectedPipe = ParsePipeIndex(path);
                    StartReading();
                    return true;
                }
            }

            return false;
        }

        public bool ReadFrame(out PipeFrame frame)
        {
            if (_frames.TryDequeue(out PipeFrame dequeued))
            {
                frame = dequeued;
                return true;
            }

            frame = default;
            return false;
        }

        public bool WriteFrame(PipeFrame frame)
        {
            if (!IsConnected)
                return false;

            try
            {
                Span<byte> header = stackalloc byte[8];
                BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)frame.Opcode);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, 4), frame.Length);

                lock (_writeLock)
                {
                    _socket!.Send(header);
                    if (frame.Data is { Length: > 0 })
                        _socket.Send(frame.Data);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to write frame: {0}", ex.Message);
                return false;
            }
        }

        public void Close()
        {
            if (_closed)
                return;

            _closed = true;

            Socket? socket = _socket;
            _socket = null;
            if (socket == null)
                return;

            try { socket.Shutdown(SocketShutdown.Both); } catch { }
            try { socket.Dispose(); } catch { }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Close();
        }

        private void StartReading()
        {
            Socket? socket = _socket;
            Thread reader = new(() => ReadLoop(socket))
            {
                IsBackground = true,
                Name = "Discord IPC Socket Read"
            };
            reader.Start();
        }

        private void ReadLoop(Socket? socket)
        {
            byte[] header = new byte[8];

            try
            {
                while (!_closed && !_disposed && socket?.Connected == true)
                {
                    if (!ReadExactly(socket, header, 0, header.Length))
                        break;

                    uint opcode = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4));
                    uint length = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4));
                    if (length > MaxFrameSize)
                        break;

                    byte[] data = new byte[length];
                    if (length > 0 && !ReadExactly(socket, data, 0, data.Length))
                        break;

                    _frames.Enqueue(new PipeFrame { Opcode = (Opcode)opcode, Data = data });
                }
            }
            catch
            {
                // Socket closed or errored; the connection is re-established by the caller.
            }
            finally
            {
                if (!_closed)
                    Close();
            }
        }

        private static bool ReadExactly(Socket socket, byte[] buffer, int offset, int count)
        {
            while (offset < count)
            {
                int read = socket.Receive(buffer, offset, count - offset, SocketFlags.None);
                if (read <= 0)
                    return false;

                offset += read;
            }

            return true;
        }

        private bool TryConnect(string path, out Socket? socket)
        {
            Socket candidate = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);

            try
            {
                candidate.Connect(new UnixDomainSocketEndPoint(path));
                socket = candidate;
                return true;
            }
            catch (SocketException) when (!File.Exists(path))
            {
                socket = null;
                return false;
            }
            catch (Exception ex)
            {
                Logger.Trace("Discord pipe {0} could not be connected: {1}", path, ex.Message);
                try { candidate.Dispose(); } catch { }
                socket = null;
                return false;
            }
        }

        private static IEnumerable<string> ResolvePipePaths(int pipe)
        {
            int start = pipe < 0 ? 0 : Math.Min(pipe, MaxPipes - 1);
            int end = pipe < 0 ? MaxPipes : start + 1;

            foreach (string dir in ResolveDirectories())
            {
                for (int i = start; i < end; i++)
                    yield return Path.Combine(dir, $"{PipePrefix}{i}");
            }
        }

        private static IEnumerable<string> ResolveDirectories()
        {
            foreach (string env in new[] { "XDG_RUNTIME_DIR", "TMPDIR", "TMP", "TEMP" })
            {
                string? value = Environment.GetEnvironmentVariable(env);
                if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
                    yield return value;
            }

            if (OperatingSystem.IsMacOS())
            {
                foreach (string dir in MacGuiTempDirectories())
                    yield return dir;
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
                            hasPipe = Directory.EnumerateFileSystemEntries(temp, PipePrefix + "*").Any();
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

        private static int ParsePipeIndex(string path)
        {
            string name = Path.GetFileName(path) ?? string.Empty;
            int dash = name.LastIndexOf('-');
            return dash >= 0 && int.TryParse(name.AsSpan(dash + 1), out int index) ? index : 0;
        }
    }
}
