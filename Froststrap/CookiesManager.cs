// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.RobloxInterfaces;
using System.Net.WebSockets;
using Froststrap.Clients.Contracts;

namespace Froststrap;

internal sealed class CookiesManager : IDisposable
{
    private CookieState _state = CookieState.Unknown;

    public EventHandler<CookieState>? StateChanged;
    public CookieState State
    {
        get => _state;
        set
        {
            _state = value;
            StateChanged?.Invoke(this, value);
        }
    }
    public bool Loaded => Enabled && State == CookieState.Success;
    private static bool Enabled => App.Settings.Prop.AllowCookieAccess;

    public AuthenticatedUser? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;

    private event EventHandler? AccountChanged;

    private const int RefreshDelayMs = 1500;

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private FileSystemWatcher? _watcher;
    private string _watchedPath = string.Empty;
    private CancellationTokenSource? _debounce;
    private DateTime _stampWrite;
    private long _stampLength = -1;
    private bool _hasLoaded;

    public void WatchAccount<T>(T subscriber, Action<T> handler) where T : class
    {
        var weak = new WeakReference<T>(subscriber);

        AccountChanged += Wrapper;

        void Wrapper(object? sender, EventArgs e)
        {
            if (weak.TryGetTarget(out T? target))
                handler(target);
            else
                AccountChanged -= Wrapper;
        }
    }

    private string AuthCookie = string.Empty;

    private const string CsrfHeader = "x-csrf-token";

    private const string TrackerCookieName = "RBXEventTrackerV2";

    private string BrowserTracker = string.Empty;
    private string _csrfToken = string.Empty;
    public string GetAuthCookie() => AuthCookie;

    public static string CookiesPath => App.Clients.Player.Cookies.CookiesPath ?? string.Empty;

    public async Task<HttpResponseMessage> AuthRequest(HttpRequestMessage request)
    {
        string? host = request.RequestUri?.Host;

        ArgumentNullException.ThrowIfNull(host);

        if (
            !host.Equals(Deployment.RobloxDomain, StringComparison.OrdinalIgnoreCase) &&
            !host.EndsWith("." + Deployment.RobloxDomain, StringComparison.OrdinalIgnoreCase)
            )
            throw new HttpRequestException($"Host must end with Roblox domain ({Deployment.RobloxDomain})");

        if (!Enabled)
            throw new InvalidOperationException("Cookie access is not enabled");

        byte[]? bufferedBody = null;
        string? contentType = null;

        if (request.Content is not null)
        {
            bufferedBody = await request.Content.ReadAsByteArrayAsync();
            contentType = request.Content.Headers.ContentType?.ToString();
        }

        HttpResponseMessage response;

        using (HttpRequestMessage first = BuildRequest(request, bufferedBody, contentType))
            response = await SendAuthenticated(first);

        if (response.StatusCode != HttpStatusCode.Forbidden
            || !response.Headers.TryGetValues(CsrfHeader, out IEnumerable<string>? tokens)
            || tokens.FirstOrDefault() is not string token
            || String.IsNullOrEmpty(token)
            || token == _csrfToken)
            return response;

        _csrfToken = token;

        response.Dispose();

        using HttpRequestMessage retry = BuildRequest(request, bufferedBody, contentType);
        return await SendAuthenticated(retry);
    }

    private async Task<HttpResponseMessage> SendAuthenticated(HttpRequestMessage request)
    {
        request.Headers.Remove(CsrfHeader);

        if (!String.IsNullOrEmpty(_csrfToken))
            request.Headers.Add(CsrfHeader, _csrfToken);

        request.Headers.Remove("Cookie");
        request.Headers.Add("Cookie", String.IsNullOrEmpty(BrowserTracker)
            ? $".ROBLOSECURITY={AuthCookie}"
            : $".ROBLOSECURITY={AuthCookie}; {TrackerCookieName}={BrowserTracker}");

        return await App.HttpClient.SendAsync(request);
    }

    private static HttpRequestMessage BuildRequest(HttpRequestMessage original, byte[]? body, string? contentType)
    {
        var request = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version
        };

        if (body is not null)
        {
            var content = new ByteArrayContent(body);

            if (!String.IsNullOrEmpty(contentType))
                content.Headers.TryAddWithoutValidation("Content-Type", contentType);

            request.Content = content;
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in original.Headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return request;
    }

    public async Task<HttpResponseMessage> AuthGet(Uri? uri)
    {
        using var request = new HttpRequestMessage { RequestUri = uri, Method = HttpMethod.Get };
        return await AuthRequest(request);
    }

    public async Task<HttpResponseMessage> AuthPost(Uri? uri, HttpContent? content)
    {
        using var request = new HttpRequestMessage { RequestUri = uri, Content = content, Method = HttpMethod.Post };
        return await AuthRequest(request);
    }

    public void AuthWebsocket(ClientWebSocket webSocket)
    {
        if (!Enabled)
            throw new InvalidOperationException("Cookie access is not enabled");

        webSocket.Options.SetRequestHeader("Cookie", $".ROBLOSECURITY={AuthCookie}");
    }

    public async Task<bool> EnsureLoadedAsync()
    {
        if (!Enabled)
            return false;

        if (!Loaded)
            await Task.Run(LoadCookies);

        return Loaded;
    }

    public async Task<AuthenticatedUser?> GetAuthenticated()
    {
        try
        {
            Uri apiUrl = UrlBuilder.BuildApiUrl("users", "v1/users/authenticated");
            HttpResponseMessage response = await AuthGet(apiUrl);
            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();
            return JsonHelper.Deserialize<AuthenticatedUser>(content);
        }
        catch (HttpRequestException ex)
        {
            App.Logger.Error($"Failed to get authenticated user: {ex.Message}");
        }

        return null;
    }

    private async Task<AuthenticatedUser?> GetAuthenticatedWithRetry()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            AuthenticatedUser? user = await GetAuthenticated();

            if (user is not null && user.Id != 0)
                return user;

            if (attempt < 2)
            {
                App.Logger.Warn($"Could not resolve the signed-in account (attempt {attempt + 1}/3), retrying");
                await Task.Delay(250 * (attempt + 1));
            }
        }

        return null;
    }

    public async Task LoadCookies()
    {
        bool accountChanged;

        await _loadLock.WaitAsync();

        try
        {
            accountChanged = await LoadTracked(false);
        }
        finally
        {
            _loadLock.Release();
        }

        if (accountChanged)
            NotifyAccountChanged();
    }

    public async Task RefreshAsync()
    {
        if (!Enabled || !_hasLoaded)
            return;

        bool accountChanged = false;

        await _loadLock.WaitAsync();

        try
        {
            if (!HasFileChanged())
                return;

            accountChanged = await LoadTracked(true);
        }
        finally
        {
            _loadLock.Release();
        }

        if (accountChanged)
            NotifyAccountChanged();
    }

    private async Task<bool> LoadTracked(bool reload)
    {
        bool hadLoaded = _hasLoaded;
        long previousUser = CurrentUser?.Id ?? 0;

        await LoadCookiesCore(reload);

        return hadLoaded && (CurrentUser?.Id ?? 0) != previousUser;
    }

    private void NotifyAccountChanged()
    {
        App.Logger.Info("Signed-in account changed");

        AccountChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task LoadCookiesCore(bool reload)
    {
        if (!Enabled)
        {
            State = CookieState.NotAllowed;
            App.Logger.Error("Cookie access not allowed");
            return;
        }

        if (!reload && CurrentUser is not null && !string.IsNullOrEmpty(AuthCookie))
        {
            App.Logger.Error("Cookie was already loaded!");
            return;
        }

        _hasLoaded = true;

        StartWatching();
        RecordStamp();

        if (!File.Exists(CookiesPath))
        {
            if (reload)
                Forget();

            State = CookieState.NotFound;
            App.Logger.Error("Cookie file not found");
            return;
        }

        try
        {
            StoredCookies result = default;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                result = await App.Clients.Player.Cookies.ReadAsync();

                if (!string.IsNullOrEmpty(result.AuthCookie))
                    break;

                if (attempt < 2)
                    await Task.Delay(200);
            }

            if (string.IsNullOrEmpty(result.AuthCookie))
            {
                if (reload)
                    Forget();

                State = CookieState.Invalid;
                return;
            }

            AuthCookie = result.AuthCookie;
            BrowserTracker = result.BrowserTracker;

            if (!string.IsNullOrEmpty(result.BrowserTracker))
                App.Logger.Info("Found a browser tracker");
            else
                App.Logger.Info("No browser tracker in the cookie store");

            AuthenticatedUser? user = await GetAuthenticatedWithRetry();

            CurrentUser = user;
            State = (user != null && user.Id != 0) ? CookieState.Success : CookieState.Invalid;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to load cookies: {ex.Message}");
            State = CookieState.Failed;
            _stampLength = -1;
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Cancel();
        _debounce?.Dispose();
        _loadLock.Dispose();

        GC.SuppressFinalize(this);
    }

    private void Forget()
    {
        AuthCookie = string.Empty;
        BrowserTracker = string.Empty;
        CurrentUser = null;
    }

    private void RecordStamp()
    {
        var info = new FileInfo(CookiesPath);

        _stampWrite = info.Exists ? info.LastWriteTimeUtc : default;
        _stampLength = info.Exists ? info.Length : -1;
    }

    private bool HasFileChanged()
    {
        var info = new FileInfo(CookiesPath);

        DateTime write = info.Exists ? info.LastWriteTimeUtc : default;
        long length = info.Exists ? info.Length : -1;

        return write != _stampWrite || length != _stampLength;
    }

    private void StartWatching()
    {
        try
        {
            string path = CookiesPath;
            string? directory = Path.GetDirectoryName(path);

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            if (_watcher is not null && _watchedPath == path)
                return;

            _watcher?.Dispose();

            _watcher = new FileSystemWatcher(directory, Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime
            };

            _watcher.Changed += (_, _) => ScheduleRefresh();
            _watcher.Created += (_, _) => ScheduleRefresh();
            _watcher.Deleted += (_, _) => ScheduleRefresh();
            _watcher.Renamed += (_, _) => ScheduleRefresh();

            _watcher.EnableRaisingEvents = true;
            _watchedPath = path;
        }
        catch (Exception ex)
        {
            App.Logger.Warn("Failed to watch the cookie file");
            App.Logger.Error(ex);
        }
    }

    private void ScheduleRefresh()
    {
        var source = new CancellationTokenSource();

        Interlocked.Exchange(ref _debounce, source)?.Cancel();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(RefreshDelayMs, source.Token);
                await RefreshAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
            }
        });
    }
}
