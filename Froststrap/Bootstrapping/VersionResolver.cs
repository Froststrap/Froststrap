// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.AppData;
using Froststrap.Clients.Model;
using Froststrap.RobloxInterfaces;

namespace Froststrap.Bootstrapping;

internal sealed record VersionResolution(string VersionGuid, Version? Version, string LaunchCommandLine, bool Stop);

internal static class VersionResolver
{
    public static async Task<VersionResolution> ResolveAsync(ClientTarget target, IAppData appData, string launchCommandLine, string versionGuid, Version? version)
    {
        // before we do anything, we need to query our channel
        // if it's set in the launch uri, we need to use it and set the registry key for it
        // else, check if the registry key for it exists, and use it

        var match = Regex.Match(
            App.LaunchSettings.RobloxLaunchArgs,
            "channel:([a-zA-Z0-9-_]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        void EnrollChannel(string channel = "production")
        {
            Deployment.Channel = channel;
            if (target == ClientTarget.Studio)
                App.Settings.Prop.StudioChannel = channel;
            else
                App.Settings.Prop.PlayerChannel = channel;
            App.Settings.Save();
        }

        void RevertChannel()
        {
            Deployment.Channel = Deployment.DefaultChannel;
            Deployment.ChannelToken = string.Empty;
            if (target == ClientTarget.Studio)
                App.Settings.Prop.StudioChannel = Deployment.DefaultChannel;
            else
                App.Settings.Prop.PlayerChannel = Deployment.DefaultChannel;
            App.Settings.Save();
        }

#pragma warning disable CA1308
        string enrolledChannel = match.Groups.Count == 2
            ? match.Groups[1].Value.ToLowerInvariant()
            : Deployment.DefaultChannel;
#pragma warning restore CA1308

        bool behindProductionCheck = App.Settings.Prop.ChannelChangeMode == ChannelChangeMode.Prompt;

        // Private channels
        string? tokenChannel = null;

        if (App.Cookies.Loaded)
        {
            UserChannel? userChannel = await Deployment.GetUserChannel(appData.BinaryType);

            if (
                userChannel?.Token is not null &&
                userChannel.AssignmentType != 1 // might need a change in future
            )
            {
                // prevent roblox from thinking its different channel
                // have to do to prevent issues with channel fflags
                if (!string.IsNullOrEmpty(enrolledChannel))
                    launchCommandLine = launchCommandLine.Replace(
                        $"channel:{enrolledChannel}",
                        $"channel:{userChannel.Channel}",
                        StringComparison.OrdinalIgnoreCase);

                Deployment.ChannelToken = userChannel.Token;
                tokenChannel = userChannel.Channel;
                enrolledChannel = userChannel.Channel;
            }
        }

        bool channelFlag = App.LaunchSettings.ChannelFlag.Active && !string.IsNullOrEmpty(App.LaunchSettings.ChannelFlag.Data);

        if (!channelFlag)
        {
            switch (App.Settings.Prop.ChannelChangeMode)
            {
                case ChannelChangeMode.Automatic:
                    App.Logger.Info("Enrolling into channel");
                    EnrollChannel(enrolledChannel);
                    break;

                case ChannelChangeMode.Prompt:
                    App.Logger.Debug("Prompting channel enrollment");

                    if (!match.Success || match.Groups.Count != 2 || string.Equals(match.Groups[1].Value, Deployment.Channel, StringComparison.OrdinalIgnoreCase))
                    {
                        App.Logger.Warn("Channel is either equal or incorrectly formatted");
                        break;
                    }

                    string displayChannel = !String.IsNullOrEmpty(match.Groups[1].Value)
                        ? match.Groups[1].Value
                        : Deployment.DefaultChannel;

                    var promptResult = await Frontend.ShowMessageBox(
                        String.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Dialog_PromptChannelChange, displayChannel, Deployment.Channel),
                        MessageBoxImage.Question,
                        MessageBoxButton.YesNo
                    );

                    if (promptResult == MessageBoxResult.Yes)
                        EnrollChannel(enrolledChannel);
                    break;

                case ChannelChangeMode.Ignore:
                    App.Logger.Debug("Ignoring channel enrollment");
                    break;
            }
        }
        else
        {
            string channelFlagData = App.LaunchSettings.ChannelFlag.Data!;
            if (String.IsNullOrEmpty(channelFlagData))
                return new VersionResolution(versionGuid, version, launchCommandLine, Stop: true);

            App.Logger.Debug($"Forcing channel {channelFlagData}");
            EnrollChannel(channelFlagData);
        }

        if (!string.IsNullOrEmpty(Deployment.ChannelToken)
            && !string.Equals(Deployment.Channel, tokenChannel, StringComparison.OrdinalIgnoreCase))
        {
            App.Logger.Info($"Dropping channel token for '{tokenChannel}' because the active channel is '{Deployment.Channel}'");
            Deployment.ChannelToken = string.Empty;
        }

        bool overrideUsed = false;

        if (target == ClientTarget.Studio && App.Settings.Prop.StudioVersionOverrideEnabled && !string.IsNullOrEmpty(App.Settings.Prop.StudioVersionOverrideHash))
        {
            versionGuid = App.Settings.Prop.StudioVersionOverrideHash.Trim();
            App.Logger.Debug($"Studio version override active: pinned to {versionGuid}");
            overrideUsed = true;
        }
        else if (target == ClientTarget.Player && App.Settings.Prop.PlayerVersionOverrideEnabled && !string.IsNullOrEmpty(App.Settings.Prop.PlayerVersionOverrideHash))
        {
            string overrideHash = App.Settings.Prop.PlayerVersionOverrideHash.Trim();
            var (valid, error) = await UI.ViewModels.Settings.ChannelViewModel.ValidateHashCore(overrideHash, true, appData.BinaryType);

            if (valid)
            {
                versionGuid = overrideHash;
                App.Logger.Debug($"Player version override active: pinned to {versionGuid}");
                overrideUsed = true;
            }
            else
            {
                App.Logger.Warn($"Player version override invalid: {error}. Falling back to channel.");

                await Frontend.ShowMessageBox(
                    string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_InvalidOverride, overrideHash, error),
                    MessageBoxImage.Warning,
                    MessageBoxButton.OK
                );

                App.Settings.Prop.PlayerVersionOverrideEnabled = false;
                App.Settings.Prop.PlayerVersionOverrideHash = string.Empty;
                App.Settings.Save();
            }
        }

        if (!overrideUsed && (!App.LaunchSettings.VersionFlag.Active || string.IsNullOrEmpty(App.LaunchSettings.VersionFlag.Data)))
        {
            ClientVersion clientVersion;

            try
            {
                clientVersion = await Deployment.GetInfo(Deployment.Channel, behindProductionCheck, false, appData.BinaryType);
            }
            catch (InvalidChannelException ex)
            {
                // If channel does not exist
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    App.Logger.Warn($"Reverting enrolled channel to {Deployment.DefaultChannel} because a WindowsPlayer build does not exist for {Deployment.Channel}");
                }
                // If channel is not available to the user (private/internal release channel)
                else if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    App.Logger.Warn($"Reverting enrolled channel to {Deployment.DefaultChannel} because {Deployment.Channel} is restricted for public use.");

                    // Only prompt if user has channel switching mode set to something other than Automatic.
                    if (App.Settings.Prop.ChannelChangeMode != ChannelChangeMode.Automatic)
                    {
                        await Frontend.ShowMessageBox(
                            String.Format(CultureInfo.InvariantCulture,
                                Strings.Bootstrapper_Dialog_UnauthorizedChannel,
                                Deployment.Channel,
                                Deployment.DefaultChannel
                            ),
                            MessageBoxImage.Information
                        );
                    }
                }
                else
                {
                    throw;
                }

                RevertChannel();
                clientVersion = await Deployment.GetInfo(Deployment.DefaultChannel, behindProductionCheck, false, appData.BinaryType);
            }

            if (clientVersion.IsBehindDefaultChannel && App.Settings.Prop.ChannelChangeMode == ChannelChangeMode.Prompt)
            {
                MessageBoxResult action = await Frontend.ShowMessageBox(
                        String.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Dialog_ChannelOutOfDate, Deployment.Channel, Deployment.DefaultChannel),
                        MessageBoxImage.Warning,
                        MessageBoxButton.YesNo
                    );

                if (action == MessageBoxResult.Yes)
                {
                    App.Logger.Debug($"Changed Roblox channel from {Deployment.Channel} to {Deployment.DefaultChannel}");

                    RevertChannel();
                    clientVersion = await Deployment.GetInfo(Deployment.DefaultChannel, behindProductionCheck: false, binaryTypeOverride: appData.BinaryType);
                }
            }

            versionGuid = clientVersion.VersionGuid;
            version = Utility.Versioning.ParseVersionSafe(clientVersion.Version);
        }
        else if (!overrideUsed)
        {
            string? versionData = App.LaunchSettings.VersionFlag.Data;
            if (string.IsNullOrEmpty(versionData))
            {
                App.Logger.Error("VersionFlag.Data was unexpectedly null or empty. Falling back to default channel.");
                var fallbackInfo = await Deployment.GetInfo(Deployment.DefaultChannel, false, false, appData.BinaryType);
                versionGuid = fallbackInfo.VersionGuid;
                version = Utility.Versioning.ParseVersionSafe(fallbackInfo.Version);
            }
            else
            {
                App.Logger.Debug($"Version set to {versionData} from arguments");
                versionGuid = versionData;
            }
        }

        return new VersionResolution(versionGuid, version, launchCommandLine, Stop: false);
    }
}
