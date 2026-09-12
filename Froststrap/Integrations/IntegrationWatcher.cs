// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.Versioning;

namespace Froststrap.Integrations
{
    [SupportedOSPlatform("windows")]
    internal class IntegrationWatcher : IDisposable
    {
        private readonly ActivityWatcher _activityWatcher;
        private readonly WindowManipulation? _windowManipulation;
        private readonly Dictionary<int, CustomIntegration> _activeIntegrations = [];

        private static bool WindowManipulationEnabled =>
            App.Settings.Prop.EnableWindowManipulation && App.Settings.Prop.EnableActivityTracking;

        public IntegrationWatcher(ActivityWatcher activityWatcher, WindowManipulation? windowManipulation = null)
        {
            _activityWatcher = activityWatcher;
            _windowManipulation = windowManipulation;

            _activityWatcher.OnGameJoin += OnGameJoin;
            _activityWatcher.OnGameLeave += OnGameLeave;
        }

        private void OnGameJoin(object? sender, EventArgs e)
        {
            if (!_activityWatcher.InGame)
                return;

            if (_windowManipulation != null && WindowManipulationEnabled && OperatingSystem.IsWindows())
            {
                Task.Run(async () =>
                {
                    if (App.Settings.Prop.AutoChangeIcon)
                    {
                        byte[]? iconBytes = await FetchGameIconAsync();
                        if (iconBytes is not null)
                            _windowManipulation.ApplyGameIcon(iconBytes);
                    }

                    if (App.Settings.Prop.AutoChangeTitle)
                    {
                        string? title = await FetchGameTitleAsync();
                        if (title is not null)
                            _windowManipulation.ApplyGameTitle(title);
                    }
                });
            }

            long currentGameId = _activityWatcher.Data.PlaceId;

            foreach (var integration in App.Settings.Prop.CustomIntegrations)
            {
                if (!integration.SpecifyGame || integration.GameID != currentGameId.ToString(CultureInfo.InvariantCulture))
                    continue;

                LaunchIntegration(integration);
            }
        }

        private void OnGameLeave(object? sender, EventArgs e)
        {
            if (_windowManipulation != null && WindowManipulationEnabled)
                _windowManipulation.ResetToConfigured();

            foreach (var pid in _activeIntegrations.Keys.ToList())
            {
                var integration = _activeIntegrations[pid];
                if (integration.AutoCloseOnGame)
                {
                    TerminateProcess(pid);
                    _activeIntegrations.Remove(pid);
                }
            }
        }

        private async Task<byte[]?> FetchGameIconAsync()
        {
            try
            {
                var activity = _activityWatcher.Data;
                if (activity is null || activity.UniverseId == 0)
                    return null;

                App.Logger.Info($"Fetching icon for Universe ID: {activity.UniverseId}");

                var request = new ThumbnailRequest
                {
                    TargetId = (ulong)activity.UniverseId,
                    Size = "150x150",
                    Type = ThumbnailType.GameIcon,
                    Format = ThumbnailFormat.Png
                };

                string? iconUrl = await Thumbnails.GetThumbnailUrlAsync(request, CancellationToken.None);
                if (string.IsNullOrEmpty(iconUrl))
                {
                    App.Logger.Info("Failed to resolve game thumbnail URL");
                    return null;
                }

                using var response = await App.HttpClient.GetAsync(new Uri(iconUrl));
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to fetch game icon: {ex.Message}");
                return null;
            }
        }

        private async Task<string?> FetchGameTitleAsync()
        {
            try
            {
                var activity = _activityWatcher.Data;
                if (activity is null)
                    return null;

                if (activity.UniverseDetails is null)
                {
                    try
                    {
                        await UniverseDetails.FetchSingle(activity.UniverseId);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error("Unhandled exception: ", ex);
                    }
                    activity.UniverseDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
                }

                if (activity.UniverseDetails?.Data is null)
                    return null;

                string gameName = activity.UniverseDetails.Data.Name;
                if (string.IsNullOrEmpty(gameName))
                    return null;

                if (App.Settings.Prop.AutoChangeTitleWithPlayerCount)
                {
                    long playing = activity.UniverseDetails.Data.Playing;
                    var converter = new UI.Converters.NumberAbbreviationConverter();
                    string abbreviated = converter.Convert(playing, typeof(string), null, CultureInfo.CurrentCulture) as string
                        ?? playing.ToString(CultureInfo.InvariantCulture);
                    return $"{gameName} ({abbreviated} playing)";
                }

                return gameName;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to fetch game title: {ex.Message}");
                return null;
            }
        }

        private void LaunchIntegration(CustomIntegration integration)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = integration.Location,
                    Arguments = integration.LaunchArgs.Replace("\r\n", " ", StringComparison.Ordinal),
                    WorkingDirectory = Path.GetDirectoryName(integration.Location),
                    UseShellExecute = true
                });

                if (process != null)
                {
                    App.Logger.Info($"Integration '{integration.Name}' launched for game ID '{integration.GameID}' (PID {process.Id}).");
                    _activeIntegrations[process.Id] = integration;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to launch integration '{integration.Name}': {ex.Message}");
            }
        }

        private static void TerminateProcess(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                process.Kill();

                App.Logger.Info($"Terminated integration process (PID {pid}).");
            }
            catch (Exception)
            {
                App.Logger.Error($"Failed to terminate process (PID {pid}), likely already exited.");
            }
        }

        public void Dispose()
        {
            foreach (var pid in _activeIntegrations.Keys)
                TerminateProcess(pid);

            _activeIntegrations.Clear();

            GC.SuppressFinalize(this);
        }
    }
}