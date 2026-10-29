// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using DiscordRPC.IO;
using DiscordRPC.Logging;

namespace Froststrap.Integrations.Discord
{
    /// <summary>
    /// Cross-platform Discord IPC pipe client.
    /// <para>
    /// The default client from DiscordRPC-NativeAOT only searches the directories
    /// supplied by <c>XDG_RUNTIME_DIR</c>/<c>TMPDIR</c>/<c>TMP</c>/<c>TEMP</c> and uses a
    /// five second timeout for every missing pipe. On macOS the socket usually lives in
    /// the GUI session temp directory (<c>/var/folders/.../T</c>), which can differ from
    /// <c>TMPDIR</c> when the app is started from a terminal or dev shell. This client
    /// scans those directories as a fallback and fails fast when a socket is missing.
    /// </para>
    /// </summary>
    internal sealed class DiscordIpcPipeClient : INamedPipeClient
    {
        private const string PipePrefix = "discord-ipc-";
        private const int MaxPipes = 10;
        private const int MaxFrameSize = 1024 * 1024;
        private const int WindowsConnectTimeout = 100;

        private static readonly string[] LinuxPackageManagerDirs =
        [
            "app/com.discordapp.Discord/",
            "snap.discord/",
            ".flatpak/dev.vencord.Vesktop/xdg-run",
        ];

        private readonly ConcurrentQueue<PipeFrame> _frames = new();
        private readonly object _writeLock = new();

        private Stream? _stream;
        private volatile bool _closed = true;
        private volatile bool _disposed;

        public ILogger Logger { get; set; } = new NullLogger();

        public bool IsConnected
        {
            get
            {
                if (_closed || _disposed)
                    return false;

                Stream? stream = Volatile.Read(ref _stream);

                if (stream is null)
                    return false;

                try
                {
                    return stream is not NamedPipeClientStream pipe || pipe.IsConnected;
                }
                catch
                {
                    return false;
                }
            }
        }

        public int ConnectedPipe { get; private set; } = -1;

        public static INamedPipeClient Create() => new DiscordIpcPipeClient();

        public bool Connect(int pipe)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (pipe > 9)
                throw new ArgumentOutOfRangeException(nameof(pipe), "Argument cannot be greater than 9");

            Close();

            int start = pipe < 0 ? 0 : pipe;
            int end = pipe < 0 ? MaxPipes : pipe + 1;

            if (OperatingSystem.IsWindows())
            {
                for (int i = start; i < end; i++)
                {
                    if (TryConnectWindows(i, out Stream? stream))
                    {
                        Start(stream, i);
                        return true;
                    }
                }

                return false;
            }

            foreach (string path in ResolveUnixPaths(start, end))
            {
                if (TryConnectUnix(path, out Stream? stream))
                {
                    Start(stream, ParsePipeIndex(path));
                    return true;
                }
            }

            return false;
        }

        public bool ReadFrame(out PipeFrame frame) => _frames.TryDequeue(out frame);

        public bool WriteFrame(PipeFrame frame)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            Stream? stream = Volatile.Read(ref _stream);

            if (_closed || stream is null)
            {
                Logger.Error("Failed to write frame because the stream is closed");
                return false;
            }

            byte[] data = frame.Data ?? [];

            try
            {
                lock (_writeLock)
                {
                    Span<byte> header = stackalloc byte[8];
                    BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)frame.Opcode);
                    BinaryPrimitives.WriteUInt32LittleEndian(header[4..], (uint)data.Length);

                    stream.Write(header);

                    if (data.Length > 0)
                        stream.Write(data);

                    stream.Flush();
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
            _closed = true;

            Stream? stream = Interlocked.Exchange(ref _stream, null);

            if (stream is null)
                return;

            try { stream.Flush(); } catch { }
            try { stream.Dispose(); } catch { }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Close();
        }

        private void Start(Stream stream, int pipe)
        {
            while (_frames.TryDequeue(out _))
            {
            }

            _stream = stream;
            ConnectedPipe = pipe;
            _closed = false;

            Thread reader = new(() => ReadLoop(stream))
            {
                IsBackground = true,
                Name = "Discord IPC Read"
            };

            reader.Start();
        }

        private void ReadLoop(Stream stream)
        {
            byte[] header = new byte[8];

            try
            {
                while (!_closed && !_disposed)
                {
                    if (!ReadExactly(stream, header, 0, header.Length))
                        break;

                    uint opcode = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4));
                    uint length = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4));

                    if (length > MaxFrameSize)
                        break;

                    byte[] data = new byte[length];

                    if (length > 0 && !ReadExactly(stream, data, 0, data.Length))
                        break;

                    _frames.Enqueue(new PipeFrame { Opcode = (Opcode)opcode, Data = data });
                }
            }
            catch (Exception ex)
            {
                Logger.Trace("Discord IPC read loop ended: {0}", ex.Message);
            }
            finally
            {
                if (ReferenceEquals(Volatile.Read(ref _stream), stream))
                    Close();
            }
        }

        private static bool ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);

                if (read <= 0)
                    return false;

                offset += read;
            }

            return true;
        }

        private bool TryConnectWindows(int pipe, [NotNullWhen(true)] out Stream? stream)
        {
            stream = null;

            NamedPipeClientStream candidate = new(".", $"{PipePrefix}{pipe}", PipeDirection.InOut, PipeOptions.Asynchronous);

            try
            {
                candidate.Connect(WindowsConnectTimeout);
                stream = candidate;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Trace("Discord pipe {0} could not be connected: {1}", pipe, ex.Message);
                candidate.Dispose();
                return false;
            }
        }

        private bool TryConnectUnix(string path, [NotNullWhen(true)] out Stream? stream)
        {
            stream = null;

            if (!File.Exists(path))
                return false;

            Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

            try
            {
                socket.Connect(new UnixDomainSocketEndPoint(path));
                stream = new NetworkStream(socket, ownsSocket: true);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Trace("Discord pipe {0} could not be connected: {1}", path, ex.Message);
                socket.Dispose();
                return false;
            }
        }

        private static IEnumerable<string> ResolveUnixPaths(int start, int end)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);

            foreach (string directory in ResolveUnixDirectories())
            {
                for (int i = start; i < end; i++)
                {
                    string path = Path.Combine(directory, $"{PipePrefix}{i}");

                    if (seen.Add(path))
                        yield return path;
                }
            }
        }

        private static IEnumerable<string> ResolveUnixDirectories()
        {
            HashSet<string> seen = new(StringComparer.Ordinal);

            foreach (string variable in new[] { "XDG_RUNTIME_DIR", "TMPDIR", "TMP", "TEMP" })
            {
                string? value = Environment.GetEnvironmentVariable(variable);

                if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value))
                    continue;

                if (seen.Add(value))
                    yield return value;

                foreach (string sub in LinuxPackageManagerDirs)
                {
                    string nested = Path.Combine(value, sub);

                    if (Directory.Exists(nested) && seen.Add(nested))
                        yield return nested;
                }
            }

            if (OperatingSystem.IsMacOS())
            {
                foreach (string directory in MacGuiTempDirectories())
                {
                    if (seen.Add(directory))
                        yield return directory;
                }
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
                            hasPipe = Directory.EnumerateFileSystemEntries(temp, $"{PipePrefix}*").Any();
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
