// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Integrations.ActivityWatcher.StudioRichPresenceUtilities;

internal sealed class HTTPServer : IDisposable
{
    private Watcher watcher { get; }
    public HTTPServer(Watcher watcher) => this.watcher = watcher;

    public bool IsDisposed;

    private HttpListener? _httpListener;
    private const int HttpPort = 4875;
    private readonly CancellationTokenSource _httpCancellationTokenSource = new();

    public void Start()
    {
        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{HttpPort}/");
            _httpListener.Start();

            _ = ListenForHTTPRequests(_httpCancellationTokenSource.Token);

            App.Logger.Info($"Studio RPC server active on port {HttpPort}");
        }
        catch (Exception ex) { App.Logger.Error(ex, "Failed to start activity HTTP server"); }
    }

    public void Stop()
    {
        _httpCancellationTokenSource.Cancel();

        if (_httpListener is null)
            return;

        try { _httpListener.Close(); }
        catch { }
        _httpListener = null;
    }

    private async Task ListenForHTTPRequests(CancellationToken token)
    {
        while (_httpListener?.IsListening == true && !token.IsCancellationRequested)
        {
            try
            {
                var context = await _httpListener.GetContextAsync().WaitAsync(token);

                _ = Task.Run(() => ProcessHTTPRequest(context), token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
                await Task.Delay(1000, token);
            }
        }
    }

    private void ProcessHTTPRequest(HttpListenerContext context)
    {
        using var response = context.Response;

        try
        {
            if (context.Request.HttpMethod != "POST" || context.Request.Url?.AbsolutePath != "/rpc")
            {
                response.StatusCode = 404;
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            string json = reader.ReadToEnd();
            var message = JsonHelper.Deserialize<StudioMessage>(json);

            if (message == null)
                return;

            if (message.StudioCommand == "SetRichPresence")
            {
                var richPresenceData = JsonHelper.Deserialize<StudioRichPresence>(message.Data);
                if (richPresenceData != null)
                    message.Data = JsonHelper.SerializeToElement(richPresenceData);
            }
            watcher.RaiseOnStudioRPCMessage(message);
            response.StatusCode = 200;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "Failed to process activity HTTP request");
            response.StatusCode = 500;
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        Stop();
        _httpCancellationTokenSource.Dispose();
    }
}