// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Security.Cryptography;
using Avalonia.Threading;
using DiscordRPC;
using DiscordRPC.IO;
using DiscordRPC.Logging;
using DiscordRPC.Message;
using Froststrap.UI;

namespace Froststrap.Integrations.Discord
{
    internal static class DiscordInvites
    {
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(5);

        private const int MaxSecretLength = 128;

        private static readonly Regex SecretPattern = new(@"^([0-9]{1,20}):([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$", RegexOptions.IgnoreCase);
        private static readonly Regex PrivateSecretPattern = new(@"^([0-9]{1,20}):private:([a-zA-Z0-9-]{1,64})(?::([a-zA-Z0-9-]{1,40}))?$");
        private static readonly Regex CodePattern = new(@"^[a-zA-Z0-9-]{1,64}$");

        private static string JoinCommand
        {
            get
            {
                if (OperatingSystem.IsMacOS())
                    return $"\\\"{Paths.Process}\\\" --discordjoin";

                return $"\"{Paths.Process}\" --discordjoin";
            }
        }

        public static bool CanHost(ActivityData activity) =>
            App.Settings.Prop.AllowDiscordInvites
            && !string.IsNullOrEmpty(activity.JobId)
            && (activity.ServerType == ServerType.Public
                || (activity.ServerType == ServerType.Private && CodePattern.IsMatch(activity.AccessCode)));

        public static Party CreateParty(ActivityData activity) => new()
        {
            ID = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(activity.JobId)))[..32],
            Size = 1,
            Max = Math.Max(activity.UniverseDetails?.Data.MaxPlayers ?? 0, 2),
            Privacy = Party.PrivacySetting.Private
        };

        public static Secrets CreateSecrets(ActivityData activity) => new()
        {
            Join = CreateSecret(activity)
        };

        private static string CreateSecret(ActivityData activity)
        {
            if (activity.ServerType != ServerType.Private)
                return $"{activity.PlaceId}:{activity.JobId}";

            string secret = $"{activity.PlaceId}:private:{activity.AccessCode}";

            if (secret.Length > MaxSecretLength)
                App.Logger.Warn("Discord invite secret exceeds the maximum length");

            return secret;
        }

        public static bool Register(DiscordRpcClient client)
        {
            try
            {
                return client.RegisterUriScheme(null, JoinCommand);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to register the Discord join command");
                return false;
            }
        }

        public static bool Join(string? secret)
        {
            Match match = SecretPattern.Match(secret ?? string.Empty);

            if (match.Success)
            {
                App.Logger.Info($"Joining {match.Groups[1].Value}/{match.Groups[2].Value} from a Discord invite");

                try
                {
                    Process.Start(Paths.Process, $"roblox://experiences/start?placeId={match.Groups[1].Value}&gameInstanceId={match.Groups[2].Value}");
                    return true;
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, "Failed to launch the game from a Discord invite");
                    return false;
                }
            }

            match = PrivateSecretPattern.Match(secret ?? string.Empty);

            if (match.Success)
            {
                App.Logger.Info($"Joining a private server in {match.Groups[1].Value} from a Discord invite");

                string deeplink = $"roblox://experiences/start?placeId={match.Groups[1].Value}&accessCode={match.Groups[2].Value}";

                if (match.Groups[3].Success)
                    deeplink += $"&linkCode={match.Groups[3].Value}";

                try
                {
                    Process.Start(Paths.Process, deeplink);
                    return true;
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, "Failed to launch the game from a Discord invite");
                    return false;
                }
            }

            App.Logger.Warn("Received an invalid join secret");

            return false;
        }

        public static bool AskToJoin(JoinRequestMessage request)
        {
            string name = string.IsNullOrEmpty(request.User.DisplayName) ? request.User.Username : request.User.DisplayName;
            string message = string.Format(CultureInfo.InvariantCulture, Strings.Dialog_DiscordInvites_JoinRequest, name);

            return Dispatcher.UIThread.InvokeAsync(async () =>
            {
                MessageBoxResult result = await Frontend.ShowMessageBox(message, MessageBoxImage.Question, MessageBoxButton.YesNo);
                return result == MessageBoxResult.Yes;
            }).GetAwaiter().GetResult();
        }

        public static async Task<string?> WaitForJoinSecret()
        {
            TaskCompletionSource<string> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource subscribed = new(TaskCreationOptions.RunContinuationsAsynchronously);

            INamedPipeClient? pipeClient = DiscordIpcPipeClient.Create();
            DiscordRpcClient client;

            try
            {
                client = new DiscordRpcClient(PlayerDiscordRichPresence.ApplicationId, -1, new NullLogger(), true, pipeClient);
                pipeClient = null;
            }
            finally
            {
                pipeClient?.Dispose();
            }

            using (client)
            {
                client.OnJoin += (_, e) => received.TrySetResult(e.Secret);
                client.OnSubscribe += (_, e) =>
                {
                    if (e.Event == EventType.Join)
                        subscribed.TrySetResult();
                };
                client.OnError += (_, e) => App.Logger.Error($"Discord RPC error while waiting for an invite - {e.Message}");

                if (!Register(client))
                    return null;

                bool initialized = client.Initialize();

                if (!initialized)
                    App.Logger.Warn("Could not connect to Discord yet, still waiting for an invite");

                client.Subscribe(EventType.Join);

                App.Logger.Info("Waiting for Discord to send the invite");

                await Task.WhenAny(received.Task, subscribed.Task, Task.Delay(ConnectTimeout));

                if (!received.Task.IsCompleted && !subscribed.Task.IsCompleted)
                {
                    App.Logger.Info("Timed out connecting to Discord");
                    return null;
                }

                if (await Task.WhenAny(received.Task, Task.Delay(JoinTimeout)) != received.Task)
                {
                    App.Logger.Info("Discord didn't send an invite");
                    return null;
                }

                return await received.Task;
            }
        }
    }
}
