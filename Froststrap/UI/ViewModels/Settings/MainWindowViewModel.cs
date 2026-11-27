// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.Elements.Settings.Pages;
using Froststrap.UI.ViewModels.Settings.FastFlags;
using Froststrap.UI.ViewModels.Settings.GlobalSettings;
using Froststrap.UI.ViewModels.Settings.Mods;
using System.Collections.ObjectModel;
using LucideAvalonia.Enum;
using System.Collections.Specialized;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings;

internal sealed class BreadcrumbItemModel
{
    public string Content { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public bool IsLast { get; set; }
}

internal sealed class MainWindowViewModel : ObservableObject
{
    private bool _isNotLinux = !OperatingSystem.IsLinux();
    public bool IsNotLinux { get => _isNotLinux; }

    private int _titlebarSpaceHeight = OperatingSystem.IsMacOS() ? 52 : 40;
    public int TitlebarSpaceHeight { get => _titlebarSpaceHeight; }
    private Thickness _titlebarPadding = OperatingSystem.IsMacOS() ? new Thickness(95, 0, 12, 0) : OperatingSystem.IsLinux() ? new Thickness(8, 0) : new Thickness(8, 0, 140, 0);
    public Thickness TitlebarPadding { get => _titlebarPadding; }
    private Thickness _openSidebarTitleMargin = OperatingSystem.IsMacOS() ? new Thickness(56, 0, 0, 0) : new Thickness(155, 0, 0, 0);
    public Thickness OpenSidebarTitleMargin { get => _openSidebarTitleMargin; }
    private int _iconButtonSize = OperatingSystem.IsMacOS() ? 28 : 24;
    public int IconButtonSize { get => _iconButtonSize; }
    private int _iconButtonIconSize = OperatingSystem.IsMacOS() ? 14 : 14;
    public int IconButtonIconSize { get => _iconButtonIconSize; }
    private int _titlebarTextSize = OperatingSystem.IsMacOS() ? 18 : 13;
    public int TitlebarTextSize { get => _titlebarTextSize; }
    public int BreadcrumbChevronSize => (int)Math.Round(_titlebarTextSize * 0.625);

    private static readonly Dictionary<string, LucideIconNames> _pageIconMap = new()
    {
        ["integrations"] = LucideIconNames.Plus,
        ["bootstrapper"] = LucideIconNames.Play,
        ["linuxsettings"] = LucideIconNames.Settings,
        ["mods"] = LucideIconNames.BookOpen,
        ["fastflags"] = LucideIconNames.Flag,
        ["appearance"] = LucideIconNames.Palette,
        ["regionselector"] = LucideIconNames.Globe,
        ["globalsettings"] = LucideIconNames.PenLine,
        ["shortcuts"] = LucideIconNames.Link2,
        ["quickplay"] = LucideIconNames.Gamepad2,
        ["channels"] = LucideIconNames.HardDriveUpload,
        ["globalsettingseditor"] = LucideIconNames.FilePenLine,
        ["fastflageditor"] = LucideIconNames.FilePenLine,
        ["communitymods"] = LucideIconNames.House,
        ["custommods"] = LucideIconNames.List,
        ["modgenerator"] = LucideIconNames.PaintbrushVertical,
    };

    private object? _currentPage;
    public object? CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    private LucideIconNames _currentPageIcon;
    public LucideIconNames CurrentPageIcon
    {
        get => _currentPageIcon;
        set => SetProperty(ref _currentPageIcon, value);
    }

    public static bool HasUnsavedChanges
    {
        get
        {
            bool dirty = false;
            void Check(string name, bool isDirty, Func<string>? detail = null)
            {
                if (!isDirty) return;
                dirty = true;
                App.Logger.Info($"Unsaved changes in {name}: {detail?.Invoke()}");
            }

            Check("Settings", App.Settings.HasUnsavedChanges, App.Settings.DescribeChanges);
            Check("State", App.State.HasUnsavedChanges, App.State.DescribeChanges);
            Check("FastFlags", App.FastFlags.HasUnsavedChanges, App.FastFlags.DescribeChanges);
            // without Sober's config there's nothing to save, so it never counts
            if (App.Clients.Player.SettingsSync is { } clientSettings)
                Check("SoberSettings", clientSettings.HasUnsavedChanges);
            Check("GlobalSettings", App.GlobalSettings.HasUnsavedChanges);
            Check("PendingSettingTasks", App.PendingSettingTasks.Count > 0,
                () => string.Join(", ", App.PendingSettingTasks.Keys));

            return dirty;
        }
    }

    private LaunchMode _selectedLaunchMode = LaunchMode.Player;

    private string _selectedPage = "integrations";
    public string SelectedPage { get => _selectedPage; set => SetProperty(ref _selectedPage, value); }

    private string _currentPageTitle = Strings.Menu_Integrations_Title;
    public string CurrentPageTitle { get => _currentPageTitle; set => SetProperty(ref _currentPageTitle, value); }

    private string _currentPageDescription = "";
    public string CurrentPageDescription { get => _currentPageDescription; set => SetProperty(ref _currentPageDescription, value); }

    public SearchBarViewModel SearchBar { get; }

    private ObservableCollection<BreadcrumbItemModel> _breadcrumbItems = [];
    public ObservableCollection<BreadcrumbItemModel> BreadcrumbItems
    {
        get => _breadcrumbItems;
        set
        {
            _breadcrumbItems?.CollectionChanged -= OnBreadcrumbsChanged;

            SetProperty(ref _breadcrumbItems, value);

            _breadcrumbItems?.CollectionChanged += OnBreadcrumbsChanged;

            UpdateBreadcrumbVisibility();
        }
    }

    private void OnBreadcrumbsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateBreadcrumbVisibility();

    private void UpdateBreadcrumbVisibility()
    {
        OnPropertyChanged(nameof(HasBreadcrumbs));
        OnPropertyChanged(nameof(ShowPageTitle));
    }

    public bool HasBreadcrumbs => BreadcrumbItems.Count > 0;
    public bool ShowPageTitle => !HasBreadcrumbs;

    public ICommand SetLaunchModeCommand { get; }

    public IRelayCommand NavigateToIntegrationsCommand { get; }
    public IRelayCommand NavigateToBootstrapperCommand { get; }
    public IRelayCommand NavigateToLinuxSettingsCommand { get; }
    public IRelayCommand NavigateToMyModsCommand { get; }
    public IRelayCommand NavigateToFastFlagsCommand { get; }
    public IRelayCommand NavigateToFastFlagEditorCommand { get; }
    public IRelayCommand NavigateToAppearanceCommand { get; }
    public IRelayCommand NavigateToRegionSelectorCommand { get; }
    public IRelayCommand NavigateToGlobalSettingsCommand { get; }
    public IRelayCommand NavigateToGlobalSettingsEditorCommand { get; }
    public IRelayCommand NavigateToShortcutsCommand { get; }
    public IRelayCommand NavigateToQuickPlayCommand { get; }
    public IRelayCommand NavigateToChannelsCommand { get; }
    public IRelayCommand NavigateToCommunityModsCommand { get; }
    public IRelayCommand NavigateToPresetModsCommand { get; }
    public IRelayCommand NavigateToModGeneratorCommand { get; }

    public ICommand OpenAboutCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand SaveAndLaunchSettingsCommand { get; }
    public ICommand RestartAppCommand { get; }
    public ICommand CloseWindowCommand { get; }
    public ICommand BreadcrumbItemClickedCommand { get; }

    public EventHandler? RequestSaveNoticeEvent;
    public EventHandler? RequestCloseWindowEvent;
    public bool GBSEnabled = App.GlobalSettings.Loaded;

    public NextAction CloseAction { get; private set; } = NextAction.Terminate;

    public MainWindowViewModel()
    {
        _breadcrumbItems.CollectionChanged += OnBreadcrumbsChanged;

        SetLaunchModeCommand = new RelayCommand<LaunchMode>(mode => SelectedLaunchMode = mode);

        OpenAboutCommand = new RelayCommand(OpenAbout);
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        SaveAndLaunchSettingsCommand = new RelayCommand(SaveAndLaunchSettings);
        RestartAppCommand = new RelayCommand(RestartApp);
        CloseWindowCommand = new RelayCommand(CloseWindow);
        BreadcrumbItemClickedCommand = new RelayCommand<BreadcrumbItemModel>(HandleBreadcrumbItemClicked);
        SearchBar = new();

        _pageFactories = new()
        {
            ["integrations"] = () => new IntegrationsViewModel(),
            ["bootstrapper"] = () => new BootstrapperViewModel(),
            ["linuxsettings"] = () => new LinuxSettingsViewModel(),
            ["mods"] = () => new ModsPresetsViewModel(),
            ["fastflags"] = () => new FastFlagsViewModel(new DefaultFastFlagsService(), new DefaultSettingsService(), new FastFlagsDialogService(this)),
            ["appearance"] = () => new AppearanceViewModel(),
            ["regionselector"] = () => new RegionSelectorViewModel(),
            ["globalsettings"] = () => new GlobalSettingsViewModel(),
            ["shortcuts"] = () => new ShortcutsViewModel(),
            ["quickplay"] = () => new QuickPlayViewModel(),
            ["channels"] = () => new ChannelViewModel(),
            ["globalsettingseditor"] = () => new GlobalSettingsEditorViewModel(this),
            ["fastflageditor"] = () => new FastFlagEditorViewModel(this),
            ["communitymods"] = () => new CommunityModsViewModel(),
            ["modgenerator"] = () => new ModGeneratorViewModel(),
            ["custommods"] = () => new ModsViewModel()
        };

        NavigateToIntegrationsCommand = new RelayCommand(() => Navigate("integrations", Strings.Menu_Integrations_Title, Strings.Menu_Integrations_Description, _pageFactories["integrations"]));
        NavigateToBootstrapperCommand = new RelayCommand(() => Navigate("bootstrapper", Strings.Menu_Bootstrapper_Title, Strings.Menu_Bootstrapper_Description, _pageFactories["bootstrapper"]));
        NavigateToLinuxSettingsCommand = new RelayCommand(() => Navigate("linuxsettings", Strings.Menu_LinuxSettings_Title, null!, _pageFactories["linuxsettings"]));
        NavigateToPresetModsCommand = new RelayCommand(() => Navigate("mods", Strings.Menu_PresetMods_Title, Strings.Menu_PresetMods_Description, _pageFactories["mods"]));
        NavigateToFastFlagsCommand = new RelayCommand(() => Navigate("fastflags", Strings.Menu_FastFlags_Title, Strings.Menu_FastFlags_Description, _pageFactories["fastflags"]));
        NavigateToAppearanceCommand = new RelayCommand(() => Navigate("appearance", Strings.Menu_Appearance_Title, Strings.Menu_Appearance_Description, _pageFactories["appearance"]));
        NavigateToRegionSelectorCommand = new RelayCommand(() => Navigate("regionselector", Strings.Menu_RegionSelector_Title, Strings.Menu_RegionSelector_Description, _pageFactories["regionselector"]));
        NavigateToGlobalSettingsCommand = new RelayCommand(() => Navigate("globalsettings", Strings.Menu_GlobalSettings_Title, Strings.Menu_GBSEditor_Description, _pageFactories["globalsettings"]));
        NavigateToShortcutsCommand = new RelayCommand(() => Navigate("shortcuts", Strings.Common_Shortcuts, Strings.Menu_Shortcuts_Description, _pageFactories["shortcuts"]));
        NavigateToQuickPlayCommand = new RelayCommand(() => Navigate("quickplay", Strings.Menu_QuickPlay_Title, Strings.Menu_QuickPlay_Description, _pageFactories["quickplay"]));
        NavigateToChannelsCommand = new RelayCommand(() => Navigate("channels", Strings.Common_Deployment, Strings.Menu_Channel_Description, _pageFactories["channels"]));

        NavigateToGlobalSettingsEditorCommand = new RelayCommand(() =>
        {
            ObservableCollection<BreadcrumbItemModel> crumbs = [
                new() { Content = Strings.Menu_GlobalSettings_Title, Tag = "globalsettings" },
                new() { Content = Strings.Common_Editor, Tag = null, IsLast = true }
            ];
            Navigate("globalsettingseditor", Strings.Common_Editor, Strings.Menu_GlobalSettingsEditor_Description, _pageFactories["globalsettingseditor"], crumbs);
        });

        NavigateToFastFlagEditorCommand = new RelayCommand(() =>
        {
            ObservableCollection<BreadcrumbItemModel> crumbs = [
                new() { Content = Strings.Menu_FastFlags_Title, Tag = "fastflags" },
                new() { Content = Strings.Common_Editor, Tag = null, IsLast = true }
            ];
            Navigate("fastflageditor", Strings.Common_Editor, Strings.Menu_FastFlagEditor_Description, _pageFactories["fastflageditor"], crumbs);
        });

        NavigateToCommunityModsCommand = new RelayCommand(() =>
        {
            ObservableCollection<BreadcrumbItemModel> crumbs = [
                new() { Content = Strings.Menu_PresetMods_Title, Tag = "mods" },
                new() { Content = Strings.Menu_CommunityMods_Title, Tag = null, IsLast = true }
            ];
            Navigate("communitymods", Strings.Menu_CommunityMods_Title, Strings.Menu_CommunityMods_Description, _pageFactories["communitymods"], crumbs);
        });

        NavigateToModGeneratorCommand = new RelayCommand(() =>
        {
            Navigate("modgenerator", Strings.Menu_ModGenerator_Title, Strings.Menu_ModGenerator_Description, _pageFactories["modgenerator"], [
                new() { Content = Strings.Menu_PresetMods_Title, Tag = "mods" },
                new() { Content = Strings.Menu_ModGenerator_Title, Tag = null, IsLast = true }
            ]);
        });

        NavigateToMyModsCommand = new RelayCommand(() =>
        {
            ObservableCollection<BreadcrumbItemModel> crumbs = [
                new() { Content = Strings.Menu_PresetMods_Title, Tag = "mods" },
                new() { Content = Strings.Menu_Mods_Title, Tag = null, IsLast = true }
            ];
            Navigate("custommods", Strings.Menu_Mods_Title, Strings.Menu_Mods_InfoBar, _pageFactories["custommods"], crumbs);
        });

        var lastPageName = App.State.Prop.LastPage;
        if (lastPageName != null)
            NavigateToLastPage(lastPageName);
        else
            NavigateToIntegrationsCommand.Execute(null);

        SelectedLaunchMode = App.State.Prop.LastLaunchMode;
    }

    private void Navigate(string pageId, string title, string description, Func<object> createViewModel, ObservableCollection<BreadcrumbItemModel>? customBreadcrumbs = null)
    {
        try
        {
            object viewModel = GetOrCreatePage(pageId, createViewModel);
            SelectedPage = pageId;
            CurrentPageTitle = title;
            CurrentPageDescription = description;
            CurrentPageIcon = _pageIconMap.TryGetValue(pageId, out var icon) ? icon : LucideIconNames.CircleQuestionMark;
            BreadcrumbItems = customBreadcrumbs ?? [];
            CurrentPage = viewModel;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex);
        }
    }

    private static readonly HashSet<string> UncachedPages = ["custommods"];

    private readonly Dictionary<string, object> _pageCache = [];

    private readonly Dictionary<string, Func<object>> _pageFactories;

    public object? PrewarmPage(string pageId)
    {
        if (UncachedPages.Contains(pageId) || _pageCache.ContainsKey(pageId) || !_pageFactories.TryGetValue(pageId, out var factory))
            return null;

        var viewModel = factory();
        _pageCache[pageId] = viewModel;
        return viewModel;
    }

    private object GetOrCreatePage(string pageId, Func<object> createViewModel)
    {
        if (UncachedPages.Contains(pageId))
            return createViewModel();

        if (_pageCache.TryGetValue(pageId, out var cached))
        {
            (cached as NotifyPropertyChangedViewModel)?.OnPropertyChanged(string.Empty);
            return cached;
        }

        var viewModel = createViewModel();
        _pageCache[pageId] = viewModel;
        return viewModel;
    }

    public bool IsCachedPage(object viewModel) => _pageCache.ContainsValue(viewModel);

    public void InvalidatePages()
    {
        foreach (var (pageId, viewModel) in _pageCache.ToList())
        {
            if (ReferenceEquals(viewModel, CurrentPage))
                continue;

            _pageCache.Remove(pageId);
            (viewModel as IDisposable)?.Dispose();
        }
    }

    public void DisposePages()
    {
        if (CurrentPage is IDisposable current && !IsCachedPage(current))
            current.Dispose();

        foreach (var viewModel in _pageCache.Values)
            (viewModel as IDisposable)?.Dispose();

        _pageCache.Clear();
    }

    private void NavigateToLastPage(string pageTypeName)
    {
        switch (pageTypeName)
        {
            case "Froststrap.UI.ViewModels.Settings.IntegrationsViewModel":
                NavigateToIntegrationsCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.BootstrapperViewModel":
                NavigateToBootstrapperCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.LinuxSettingsViewModel":
                if (GlobalViewModel.HasClientSettingsPage)
                    NavigateToLinuxSettingsCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.Mods.ModsViewModel":
                NavigateToMyModsCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.FastFlagsViewModel":
                NavigateToFastFlagsCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.AppearanceViewModel":
                NavigateToAppearanceCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.GlobalSettingsViewModel":
                if (GBSEnabled)
                    NavigateToGlobalSettingsCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.ShortcutsViewModel":
                NavigateToShortcutsCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.QuickPlayViewModel":
                NavigateToQuickPlayCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.ChannelViewModel":
                NavigateToChannelsCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.Mods.CommunityModsViewModel":
                NavigateToCommunityModsCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.ModsPresetsViewModel":
                NavigateToPresetModsCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.Mods.ModGeneratorViewModel":
                NavigateToModGeneratorCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.RegionSelectorViewModel":
                NavigateToRegionSelectorCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.FastFlags.FastFlagEditorViewModel":
                NavigateToFastFlagEditorCommand.Execute(null);
                break;
            case "Froststrap.UI.ViewModels.Settings.GlobalSettings.GlobalSettingsEditorViewModel":
                if (GBSEnabled)
                    NavigateToGlobalSettingsEditorCommand.Execute(null);
                break;
            default:
                NavigateToIntegrationsCommand.Execute(null);
                break;
        }
    }

    private void OpenAbout()
    {
        if (App.AboutOpen) return;

        App.FrostRPC?.SetDialog("About");
        new Elements.About.MainWindow().Show();
        App.FrostRPC?.ClearDialog();

        App.AboutOpen = true;
    }

    private void CloseWindow() => RequestCloseWindowEvent?.Invoke(this, EventArgs.Empty);

    public void SaveSettings()
    {
        if (CurrentPage != null)
        {
            App.State.Prop.LastPage = CurrentPage.GetType().FullName;
        }

        App.Settings.Save();
        App.State.Save();
        App.FastFlags.Save();
        App.GlobalSettings.Save();

        App.Clients.Player.SettingsSync?.Save();

        foreach (var pair in App.PendingSettingTasks)
        {
            var task = pair.Value;

            if (task.Changed)
            {
                App.Logger.Info($"Executing pending task '{task}'");
                task.Execute();
            }
        }

        App.PendingSettingTasks.Clear();
        RequestSaveNoticeEvent?.Invoke(this, EventArgs.Empty);
    }

    public void SaveAndLaunchSettings()
    {
        SaveSettings();
        LaunchHandler.ProcessNextAction(LaunchEntry.ToNextAction(SelectedLaunchMode));
    }

    public LaunchMode SelectedLaunchMode
    {
        get => _selectedLaunchMode;
        set
        {
            if (SetProperty(ref _selectedLaunchMode, value))
            {
                App.State.Prop.LastLaunchMode = value;
                App.State.SaveSetting("LastLaunchMode");
                OnPropertyChanged(nameof(LaunchButtonText));
            }
        }
    }

    public static bool IsPlayerInstalled => App.IsPlayerInstalled;

    public static bool IsStudioInstalled => App.IsStudioInstalled;
    public static string PlayerMenuItemText => App.Clients.Player.LaunchLabel;

    public string LaunchButtonText
    {
        get
        {
            if (SelectedLaunchMode == LaunchMode.Player)
            {
                string modeName = App.Clients.Player.LaunchLabel;
                return IsPlayerInstalled
                    ? $"{Strings.Common_SaveAndLaunch} {modeName}"
                    : $"{Strings.Common_SaveAndInstall} {modeName}";
            }
            else
            {
                return IsStudioInstalled
                    ? $"{Strings.Common_SaveAndLaunch} {Strings.Common_Studio}"
                    : $"{Strings.Common_SaveAndInstall} {Strings.Common_Studio}";
            }
        }
    }

    public void RestartApp()
    {
        SaveSettings();

        CloseAction = NextAction.LaunchSettings;
        CloseWindow();
    }

    private void HandleBreadcrumbItemClicked(BreadcrumbItemModel? item)
    {
        if (item?.Tag == null || item.IsLast) return;

        switch (item.Tag)
        {
            case "mods":
                NavigateToPresetModsCommand.Execute(null);
                break;
            case "fastflags":
                NavigateToFastFlagsCommand.Execute(null);
                break;
            case "globalsettings":
                NavigateToGlobalSettingsCommand.Execute(null);
                break;
        }
    }

    public void SetSearchIndex(List<SearchBarItem> searchIndex)
    {
        SearchBar.SetSearchIndex(searchIndex);
    }

    public List<SearchBarItem> GetSearchIndex()
    {
        return SearchBar.GetSearchIndex();
    }
}
