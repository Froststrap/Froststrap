using Froststrap.RobloxInterfaces;

namespace Froststrap.UI.ViewModels.Settings
{
    public class ChannelViewModel : NotifyPropertyChangedViewModel
    {
        private CancellationTokenSource? _cts;

        public ChannelViewModel()
        {
            Task.Run(() => LoadChannelDeployInfo(App.Settings.Prop.Channel));
        }

        public IEnumerable<UpdateCheck> UpdateCheckValues => Enum.GetValues(typeof(UpdateCheck)).Cast<UpdateCheck>();

        public UpdateCheck SelectedUpdateCheck
        {
            get => App.Settings.Prop.UpdateChecks;
            set
            {
                App.Settings.Prop.UpdateChecks = value;
                OnPropertyChanged(nameof(SelectedUpdateCheck));
            }
        }

        public bool IsRobloxInstallationMissing => !App.IsPlayerInstalled && !App.IsStudioInstalled;

        private async Task LoadChannelDeployInfo(string channel)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                ShowLoadingError = false;
                OnPropertyChanged(nameof(ShowLoadingError));

                ChannelInfoLoadingText = Strings.Menu_Channel_Switcher_Fetching;
                OnPropertyChanged(nameof(ChannelInfoLoadingText));

                ChannelDeployInfo = null;
                OnPropertyChanged(nameof(ChannelDeployInfo));

                if (token.IsCancellationRequested) return;

                bool isPrivate = await Deployment.IsChannelPrivate(channel);

                if (token.IsCancellationRequested) return;

                if (App.Cookies.Loaded && isPrivate && string.IsNullOrEmpty(Deployment.ChannelToken))
                {
                    UserChannel? userChannel = await Deployment.GetUserChannel("WindowsPlayer");
                    if (userChannel?.Token is not null)
                        Deployment.ChannelToken = userChannel.Token;
                }

                ClientVersion info = await Deployment.GetInfo(channel, true, true);

                if (token.IsCancellationRequested) return;

                ShowChannelWarning = info.IsBehindDefaultChannel;
                OnPropertyChanged(nameof(ShowChannelWarning));

                ChannelDeployInfo = new DeployInfo
                {
                    Version = info.Version,
                    VersionGuid = isPrivate ? "version-private" : info.VersionGuid,
                    Timestamp = info.Timestamp?.ToLocalTime().ToString() ?? "?"
                };

                App.State.Prop.IgnoreOutdatedChannel = true;
                OnPropertyChanged(nameof(ChannelDeployInfo));
            }
            catch (OperationCanceledException) { /* Do nothing, task was replaced */ }
            catch (InvalidChannelException ex)
            {
                if (token.IsCancellationRequested) return;

                ShowLoadingError = true;
                OnPropertyChanged(nameof(ShowLoadingError));

                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                    ChannelInfoLoadingText = Strings.Menu_Channel_Switcher_Unauthorized;
                else
                    ChannelInfoLoadingText = $"An http error has occured ({ex.StatusCode})";

                OnPropertyChanged(nameof(ChannelInfoLoadingText));
            }
        }

        public bool ShowLoadingError { get; set; } = false;
        public bool ShowChannelWarning { get; set; } = false;

        public DeployInfo? ChannelDeployInfo { get; private set; } = null;
        public string ChannelInfoLoadingText { get; private set; } = null!;

        public string ViewChannel
        {
            get => App.Settings.Prop.Channel;
            set
            {
                value = value.Trim();

                _ = LoadChannelDeployInfo(value);

                if (value.ToLower() == "live" || value.ToLower() == "zlive")
                {
                    App.Settings.Prop.Channel = Deployment.DefaultChannel;
                }
                else
                {
                    App.Settings.Prop.Channel = value;
                }

                OnPropertyChanged(nameof(ViewChannel));
            }
        }

        public bool UpdateRoblox
        {
            get => App.Settings.Prop.UpdateRoblox && !IsRobloxInstallationMissing;
            set => App.Settings.Prop.UpdateRoblox = value;
        }

        public bool StaticDirectory
        {
            get => App.Settings.Prop.StaticDirectory;
            set => App.Settings.Prop.StaticDirectory = value;
        }

        public bool SaveAndLaunchToPlayer
        {
            get => App.Settings.Prop.SaveAndLaunchToPlayer;
            set => App.Settings.Prop.SaveAndLaunchToPlayer = value;
        }

        public IReadOnlyDictionary<string, ChannelChangeMode> ChannelChangeModes => new Dictionary<string, ChannelChangeMode>
        {
            { Strings.Menu_Channel_ChangeAction_Automatic, ChannelChangeMode.Automatic },
            { Strings.Menu_Channel_ChangeAction_Prompt, ChannelChangeMode.Prompt },
            { Strings.Menu_Channel_ChangeAction_Ignore, ChannelChangeMode.Ignore },
        };

        public string SelectedChannelChangeMode
        {
            get => ChannelChangeModes.FirstOrDefault(x => x.Value == App.Settings.Prop.ChannelChangeMode).Key;
            set => App.Settings.Prop.ChannelChangeMode = ChannelChangeModes[value];
        }

        public bool ForceRobloxReinstallation
        {
            get => App.State.Prop.ForceReinstall || IsRobloxInstallationMissing;
            set => App.State.Prop.ForceReinstall = value;
        }
    }
}