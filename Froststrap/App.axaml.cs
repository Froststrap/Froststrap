using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Froststrap.AvaloniaUI.ViewModels;
using Froststrap.AvaloniaUI.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bloxstrap.Integrations;
using Froststrap.Enums;
using Froststrap.Models.Attributes;
using Froststrap.Models.Persistable;
using Froststrap.Models.SettingTasks.Base;

namespace Froststrap.AvaloniaUI
{
    public partial class App : Application
    {
#if QA_BUILD
        public const string ProjectName = "Froststrap-QA";
#else
        public const string ProjectName = "Froststrap";
#endif
        public const string ProjectOwner = "RealMeddsam";
        public const string ProjectRepository = "RealMeddsam/Froststrap";
        public const string ProjectHelpLink = "https://github.com/bloxstraplabs/bloxstrap/wiki";
        public const string ProjectSupportLink = "https://github.com/RealMeddsam/Froststrap/issues/new";
        public const string ProjectRemoteDataLink = "https://raw.githubusercontent.com/RealMeddsam/config/refs/heads/main/Data.json";

        // Currently a remote data because we plan on moving to a GitHub org, so users will not need to manually update
        public static string ProjectDownloadLink => RemoteData.Prop.ProjectDownloadLink ?? "https://github.com/RealMeddsam/Froststrap/releases";

        
        public const string RobloxPlayerAppName = "RobloxPlayerBeta.exe";
        public const string RobloxStudioAppName = "RobloxStudioBeta.exe";

        public const string RobloxAnselAppName = "eurotrucks2.exe";

        // I'll add cross platform fields later
        public const string UninstallKey = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProjectName}";

        public const string ApisKey = $"Software\\{ProjectName}";
        public static LaunchSettings LaunchSettings { get; private set; } = null!;
        public static BuildMetadataAttribute BuildMetadata = Assembly.GetExecutingAssembly().GetCustomAttribute<BuildMetadataAttribute>()!;
        public static string Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString();
        public static Bootstrapper? Bootstrapper { get; set; } = null!;
        public FroststrapRichPresence RichPresence { get; private set; } = null!;
        private static bool _showingExceptionDialog = false;
        private static string? _webUrl = null;
        
        public static bool IsStudioVisible => !String.IsNullOrEmpty(RobloxState.Prop.Studio.VersionGuid);
        public static readonly MD5 MD5Provider = MD5.Create();

        
        public static Logger Logger = new();
        public static readonly Dictionary<string, BaseTask> PendingSettingTasks = new();
        public static readonly JsonManager<Settings> Settings = new();
        public static readonly JsonManager<State> State = new();
        public static readonly JsonManager<RobloxState> RobloxState = new();
        public static readonly RemoteDataManager RemoteData = new();
        public static readonly FastFlagManager FastFlags = new();
        public static readonly GBSEditor GlobalSettings = new();
        public static readonly CookiesManager Cookies = new();
        public static readonly HttpClient HttpClient = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });

        public static string WebUrl
        {
            get
            {
                if (_webUrl != null) return _webUrl;
                string url = ConstructBloxstrapWebUrl();
                if (Settings.Loaded) _webUrl = url;
                return url;
            }
        }

        public static bool IsActionBuild => !string.IsNullOrEmpty(BuildMetadata.CommitRef);
        public static bool IsProductionBuild => IsActionBuild && BuildMetadata.CommitRef.StartsWith("tag", StringComparison.Ordinal);

        public static FroststrapRichPresence? FrostRPC
        {
            get => (Current as App)?.RichPresence;
            set
            {
                if (Current is App app) app.RichPresence = value!;
            }
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            Locale.Initialize();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel()
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        #region Logging & Termination

        public static void Terminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;
            Logger.WriteLine("App::Terminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");
            Environment.Exit(exitCodeNum);
        }

        public static void SoftTerminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;
            Logger.WriteLine("App::SoftTerminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

            if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        }

        public static void FinalizeExceptionHandling(Exception ex, bool log = true)
        {
            if (log) Logger.WriteException("App::FinalizeExceptionHandling", ex);
            if (_showingExceptionDialog) return;

            _showingExceptionDialog = true;
            SendLog();
            // stub: Avalonia message box
            Console.WriteLine("Exception: " + ex);

            Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
        }

        #endregion

        #region Helpers

        public static string ConstructBloxstrapWebUrl()
        {
            if (Settings.Prop.WebEnvironment == WebEnvironment.Production || !Settings.Prop.DeveloperMode)
                return "bloxstraplabs.com";

            string? sub = Settings.Prop.WebEnvironment.GetDescription();
            return $"web-{sub}.bloxstraplabs.com";
        }

        public static async void SendLog()
        {
            if (!Settings.Prop.EnableAnalytics || !IsProductionBuild) return;
            try
            {
                await HttpClient.PostAsync($"https://{WebUrl}/metrics/post-exception",
                    new StringContent(Logger.AsDocument));
            }
            catch (Exception ex)
            {
                Logger.WriteException("App::SendLog", ex);
            }
        }

        #endregion
    }

    public static class Locale
    {
        public static CultureInfo CurrentCulture { get; set; } = CultureInfo.InvariantCulture;

        public static void Set(string cultureName)
        {
            try
            {
                CurrentCulture = new CultureInfo(cultureName);
                CultureInfo.CurrentCulture = CurrentCulture;
                CultureInfo.CurrentUICulture = CurrentCulture;
            }
            catch
            {
                CurrentCulture = CultureInfo.InvariantCulture;
            }
        }

        public static void Initialize()
        {
            Set(CultureInfo.CurrentCulture.Name);
        }

        public static readonly Dictionary<string, string> SupportedLocales = new()
        {
            { "en-US", "English (US)" },
            { "nil", "Default" }
        };
    }
}
