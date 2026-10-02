// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using Froststrap.Integrations.AccountManager;
using Froststrap.Utility;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Dialogs;

internal class PrivateServerInfo : NotifyPropertyChangedViewModel
{
    public PrivateServerInfo(
        long vipServerId,
        string accessCode,
        string name,
        long ownerId,
        string ownerName,
        string? ownerAvatarUrl,
        int maxPlayers,
        int currentPlayers)
    {
        VipServerId = vipServerId;
        AccessCode = accessCode;
        Name = name;
        OwnerId = ownerId;
        OwnerName = ownerName;
        OwnerAvatarUrl = ownerAvatarUrl;
        MaxPlayers = maxPlayers;
        CurrentPlayers = currentPlayers;
    }

    public long VipServerId { get; }
    public string AccessCode { get; }
    public string Name { get; }
    public long OwnerId { get; }
    public string OwnerName { get; }
    public int MaxPlayers { get; }
    public int CurrentPlayers { get; }

    private string? _ownerAvatarUrl;
    public string? OwnerAvatarUrl
    {
        get => _ownerAvatarUrl;
        set
        {
            if (!SetProperty(ref _ownerAvatarUrl, value))
                return;

            _ = LoadOwnerAvatarBitmapAsync();
        }
    }

    private Bitmap? _ownerAvatarBitmap;
    public Bitmap? OwnerAvatarBitmap
    {
        get => _ownerAvatarBitmap;
        private set => SetProperty(ref _ownerAvatarBitmap, value);
    }

    private bool _loadingOwnerAvatar;
    private string? _loadedOwnerAvatarUrl;

    private async Task LoadOwnerAvatarBitmapAsync()
    {
        string? url = _ownerAvatarUrl;

        if (_loadingOwnerAvatar || String.IsNullOrEmpty(url) || _loadedOwnerAvatarUrl == url)
            return;

        _loadingOwnerAvatar = true;

        try
        {
            Bitmap? bitmap = await ImageLoader.LoadAsync(url, 80);

            if (bitmap is null)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_ownerAvatarUrl == url)
                {
                    OwnerAvatarBitmap = bitmap;
                    _loadedOwnerAvatarUrl = url;
                }
            });
        }
        finally
        {
            _loadingOwnerAvatar = false;
        }
    }
}

internal class PrivateServerJoinDialogViewModel : NotifyPropertyChangedViewModel
{
    private readonly long _placeId;

    private bool _isLoading;
    private bool _isEmpty;
    private ObservableCollection<PrivateServerInfo> _servers = [];

    public PrivateServerJoinDialogViewModel(long placeId)
    {
        _placeId = placeId;
        JoinPrivateServerCommand = new RelayCommand<string>(OnJoinPrivateServer);
        _ = LoadAsync();
    }

    public ObservableCollection<PrivateServerInfo> Servers
    {
        get => _servers;
        private set => SetProperty(ref _servers, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    public ICommand JoinPrivateServerCommand { get; }

    private async Task LoadAsync()
    {
        if (_placeId == 0) return;

        var accountManager = AccountManager.Shared;
        if (accountManager?.ActiveAccount == null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsEmpty = true);
            return;
        }

        string? cookie = accountManager.GetRoblosecurityForUser(accountManager.ActiveAccount.UserId);
        if (string.IsNullOrEmpty(cookie))
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsEmpty = true);
            return;
        }

        IsLoading = true;
        try
        {
            Uri url = UrlBuilder.BuildApiUrl(
                "games",
                $"v1/games/{_placeId}/private-servers?excludeFriendServers=false&sortOrder=Asc"
            );

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
            request.Headers.Add("Origin", "https://www.roblox.com");
            request.Headers.Add("Referrer", "https://www.roblox.com");

            var response = await Http.SendJson<PrivateServersResponse>(request);
            if (response?.Data == null || response.Data.Count == 0)
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsEmpty = true);
                return;
            }

            var ownerIds = response.Data
                .Select(s => s.Owner.Id)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            var avatarUrls = new Dictionary<long, string?>();
            if (ownerIds.Count > 0)
            {
                avatarUrls = await accountManager.GetAvatarUrlsBulkAsync(ownerIds);
            }

            var servers = new List<PrivateServerInfo>();
            foreach (var server in response.Data)
            {
                servers.Add(new PrivateServerInfo(
                    server.VipServerId,
                    server.AccessCode,
                    server.Name,
                    server.Owner.Id,
                    server.Owner.Name,
                    avatarUrls.GetValueOrDefault(server.Owner.Id),
                    server.MaxPlayers,
                    server.Players.Count
                ));
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Servers = new ObservableCollection<PrivateServerInfo>(servers);
                IsEmpty = servers.Count == 0;
            });
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Private server fetch failed: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() => IsEmpty = true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnJoinPrivateServer(string? accessCode)
    {
        if (string.IsNullOrWhiteSpace(accessCode)) return;
        Launch(_placeId, accessCode: accessCode);
    }

    private static void Launch(long placeId, string? jobId = null, string? accessCode = null)
    {
        if (placeId == 0) return;

        string deeplink = $"roblox://experiences/start?placeId={placeId}";

        if (!string.IsNullOrEmpty(accessCode))
            deeplink += "&accessCode=" + Uri.EscapeDataString(accessCode);
        else if (!string.IsNullOrEmpty(jobId))
            deeplink += "&gameInstanceId=" + Uri.EscapeDataString(jobId);

        Process.Start(new ProcessStartInfo(deeplink) { UseShellExecute = true });
    }
}