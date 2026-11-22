// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Web;

namespace Froststrap.Bootstrapping;

internal sealed class JoinPreparation(GameJoinData joinData, Func<IBootstrapperDialog?> dialog, BootstrapperProgress reporter) : IDisposable
{
    private bool _matchmakingInProgress;
    private volatile bool _skipMatchmaking;
    private CancellationTokenSource? _matchmakingCts;

    private IBootstrapperDialog? Dialog => dialog();

    public async Task<string> PrepareAsync(string launchCommandLine)
    {
        // reapplied on every launch because roblox can reset the file on its own.
        AppStorageManager.Apply();

        if (joinData.JoinType == GameJoinType.Unknown)
            App.Logger.Warn("Unable to get join data");

        App.Logger.Info($"Join Type: {joinData.JoinType}");
        App.Logger.Info($"Join Origin: {joinData.JoinOrigin ?? "null"}");
        App.Logger.Info($"Place ID: {joinData.PlaceId?.ToString(CultureInfo.InvariantCulture) ?? "null"}");
        App.Logger.Info($"Job ID: {joinData.JobId ?? "null"}");
        App.Logger.Info($"Access Code: {joinData.AccessCode ?? "null"}");

        if (launchCommandLine.StartsWith("roblox://", StringComparison.Ordinal))
        {
            App.Logger.Info("Joining through roblox:// URI, skipping Better Matchmaking");
            return launchCommandLine;
        }

        bool isFollowUser = false;

        // joinData.JoinType == GameJoinType.RequestFollowUser just doesnt work at all
        // idk why they dont use it when the user is following a friend, but ok
        if (App.Settings.Prop.EnableBetterMatchmaking &&
            (joinData.JoinOrigin == "friendServerListJoin" || joinData.JoinOrigin == "placesListInHomePage"))
        {
            App.Logger.Debug("User is trying to join a friend, showing dialog");

            var result = await Frontend.ShowMessageBox(
                String.Format(CultureInfo.InvariantCulture, Strings.Menu_Bootstrapper_Experimental_BetterMatchmaking_FollowUser),
                MessageBoxImage.Question,
                MessageBoxButton.YesNo
            );

            isFollowUser = result == MessageBoxResult.Yes;
        }

        string? serverid = null;
        bool matchmakingCancelled = false;

        _matchmakingInProgress = true;
        _skipMatchmaking = false;
        _matchmakingCts = new CancellationTokenSource();

        Dialog?.CancelButtonText = Strings.Bootstrapper_CancelButton_Skip;

        try
        {
            if (App.Settings.Prop.EnableBetterMatchmaking &&
                joinData.JoinType == GameJoinType.RequestGame &&
                joinData.PlaceId != null &&
                !isFollowUser)
            {
                if (_skipMatchmaking)
                {
                    App.Logger.Info("Matchmaking was skipped due to user cancellation.");
                    matchmakingCancelled = true;
                }
                else
                {
                    serverid = await Integrations.BetterMatchmaking.FindServerAsync((long)joinData.PlaceId, reporter.SetStatus, _matchmakingCts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            App.Logger.Info("Better Matchmaking was skipped, joining original server.");
            matchmakingCancelled = true;
        }
        catch (HttpRequestException ex)
        {
            _ = Frontend.ShowConnectivityDialog(
                String.Format(CultureInfo.InvariantCulture, Strings.Dialog_Connectivity_UnableToConnect, "rovalra.com"),
                Strings.Dialog_Connectivity_MatchmakingFailed,
                MessageBoxImage.Warning,
                ex
            );
        }
        finally
        {
            Dialog?.CancelButtonText = Strings.Common_Cancel;
            _matchmakingInProgress = false;
            _matchmakingCts?.Dispose();
            _matchmakingCts = null;
        }

        if (!matchmakingCancelled && !string.IsNullOrEmpty(serverid) && joinData.PlaceId is not null)
        {
            string placeLauncherUrl = UrlBuilder.BuildPlacelauncherUrl((long)joinData.PlaceId, serverid);
            launchCommandLine = launchCommandLine.Replace(joinData.PlaceLauncherUrl, HttpUtility.UrlEncode(placeLauncherUrl), StringComparison.Ordinal);
            joinData.JobId = serverid;
        }

        return launchCommandLine;
    }

    public bool TrySkipMatchmaking()
    {
        if (_matchmakingInProgress)
        {
            App.Logger.Info("Skipping Better MatchMaking.");
            _skipMatchmaking = true;
            _matchmakingCts?.Cancel();
            reporter.SetStatus(Strings.Bootstrapper_Status_SkippingMatchmaking);
            return true;
        }

        return false;
    }

    public void Dispose() => _matchmakingCts?.Dispose();
}
