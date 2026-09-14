// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Dialogs;

internal class SubplaceJoinDialogViewModel : NotifyPropertyChangedViewModel
{
    private readonly long _universeId;

    private bool _isLoading;
    private ObservableCollection<PlaceInfo> _subplaces = [];

    public SubplaceJoinDialogViewModel(long universeId)
    {
        _universeId = universeId;
        JoinSubplaceCommand = new RelayCommand<PlaceInfo>(OnJoinSubplace);
        _ = LoadAsync();
    }

    public ObservableCollection<PlaceInfo> Subplaces
    {
        get => _subplaces;
        private set => SetProperty(ref _subplaces, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public ICommand JoinSubplaceCommand { get; }

    private async Task LoadAsync()
    {
        if (_universeId == 0) return;

        IsLoading = true;
        try
        {
            Uri url = UrlBuilder.BuildApiUrl(
                "develop",
                $"v1/universes/{_universeId}/places?isUniverseCreation=false&limit=100&sortOrder=Asc"
            );

            var response = await Http.GetJson<SubplacesResponse>(url);
            if (response?.Data == null || response.Data.Count == 0)
                return;

            var tempSubplaces = response.Data
                .Select(place => new PlaceInfo(place.Id, place.UniverseId, place.Name, ""))
                .ToList();

            var thumbRequests = tempSubplaces.Select(p => new ThumbnailRequest
            {
                TargetId = (ulong)p.Id,
                Type = ThumbnailType.PlaceIcon,
                Size = "150x150",
                Format = ThumbnailFormat.Png
            }).ToList();

            try
            {
                var urls = await Thumbnails.GetThumbnailUrlsAsync(thumbRequests, CancellationToken.None);
                for (int i = 0; i < tempSubplaces.Count; i++)
                    tempSubplaces[i].ThumbnailUrl = urls.ElementAtOrDefault(i) ?? "";
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Subplace thumbnail fetch failed: {ex.Message}");
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Subplaces = new ObservableCollection<PlaceInfo>(tempSubplaces);
            });
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Subplace fetch failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnJoinSubplace(PlaceInfo? subplace)
    {
        if (subplace != null)
            Launch(subplace.Id);
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