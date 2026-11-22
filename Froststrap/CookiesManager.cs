// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.RobloxInterfaces;

namespace Froststrap;

internal sealed class CookiesManager
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

    private string AuthCookie = string.Empty;

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

        request.Headers.Add("Cookie", $".ROBLOSECURITY={AuthCookie}");
        return await App.HttpClient.SendAsync(request);
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

    public async Task LoadCookies()
    {
        // we use the status to infrom user about it in the menu
        if (!Enabled)
        {
            State = CookieState.NotAllowed;
            App.Logger.Error("Cookie access not allowed");
            return;
        }

        if (!string.IsNullOrEmpty(AuthCookie))
        {
            App.Logger.Error("Cookie was already loaded!");
            return;
        }

        if (!File.Exists(CookiesPath))
        {
            State = CookieState.NotFound;
            App.Logger.Error("Cookie file not found");
            return;
        }

        try
        {
            string authCookie = await App.Clients.Player.Cookies.ReadAuthCookieAsync();

            if (string.IsNullOrEmpty(authCookie))
            {
                State = CookieState.Invalid;
                return;
            }

            AuthCookie = authCookie;
            AuthenticatedUser? user = await GetAuthenticated();
            State = (user != null && user.Id != 0) ? CookieState.Success : CookieState.Invalid;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to load cookies: {ex.Message}");
            State = CookieState.Failed;
        }
    }
}
