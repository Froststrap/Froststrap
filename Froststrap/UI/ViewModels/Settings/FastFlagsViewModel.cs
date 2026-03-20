using CommunityToolkit.Mvvm.Input;
using Froststrap.Enums.FlagPresets;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings
{
    public interface IFastFlagsService
    {
        string? GetPreset(string key);
        void SetPreset(string key, string? value);
        object? GetPresetEnum(IReadOnlyDictionary<object, string> enumMap, string key, string defaultValue);
        void SetPresetEnum(string key, string value, string defaultValue);
        IReadOnlyDictionary<string, object> GetAllPresets();
        void SetAllPresets(IReadOnlyDictionary<string, object> presets);
    }

    public interface ISettingsService
    {
        bool UseFastFlagManager { get; set; }
    }

    public interface IDialogService
    {
        Task OpenFastFlagEditorAsync();
    }

    public class FastFlagsViewModel : NotifyPropertyChangedViewModel
    {
        private readonly IFastFlagsService _flagsService;
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private Dictionary<string, object>? _preResetFlags;

        public event EventHandler? RequestPageReloadEvent;

        public FastFlagsViewModel() 
            : this(
                new DefaultFastFlagsService(),
                new DefaultSettingsService(),
                new DefaultDialogService())
        {
        }

        public FastFlagsViewModel(
            IFastFlagsService flagsService,
            ISettingsService settingsService,
            IDialogService dialogService)
        {
            _flagsService = flagsService ?? throw new ArgumentNullException(nameof(flagsService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public ICommand OpenFastFlagEditorCommand => new AsyncRelayCommand(async () =>
        {
            App.Logger.WriteLine("FastFlagsViewModel", "OpenFastFlagEditorCommand executed");
            await _dialogService.OpenFastFlagEditorAsync();
            App.Logger.WriteLine("FastFlagsViewModel", "OpenFastFlagEditorCommand completed");
        });

        public bool RemoveGrass
        {
            get => _flagsService.GetPreset("Rendering.RemoveGrass1") == "0";
            set
            {
                _flagsService.SetPreset("Rendering.RemoveGrass1", value ? "0" : null);
                _flagsService.SetPreset("Rendering.RemoveGrass2", value ? "0" : null);
                _flagsService.SetPreset("Rendering.RemoveGrass3", value ? "0" : null);
                OnPropertyChanged();
            }
        }

        public bool LowPolyMeshesEnabled
        {
            get => _flagsService.GetPreset("Rendering.LowPolyMeshes1") != null;
            set
            {
                if (value)
                {
                    LowPolyMeshesLevel = 5;
                }
                else
                {
                    _flagsService.SetPreset("Rendering.LowPolyMeshes1", null);
                    _flagsService.SetPreset("Rendering.LowPolyMeshes2", null);
                    _flagsService.SetPreset("Rendering.LowPolyMeshes3", null);
                    _flagsService.SetPreset("Rendering.LowPolyMeshes4", null);
                }
                OnPropertyChanged();
            }
        }

        public int LowPolyMeshesLevel
        {
            get
            {
                if (int.TryParse(_flagsService.GetPreset("Rendering.LowPolyMeshes1"), out var storedValue))
                {
                    return (storedValue * 9) / 2000;
                }
                return 0;
            }
            set
            {
                int clamped = Math.Clamp(value, 0, 9);

                int[] baseValues = { 2000, 1500, 1000, 500 };
                int[] levels = new int[4];

                for (int i = 0; i < 4; i++)
                {
                    levels[i] = (baseValues[i] * clamped) / 9;
                }

                _flagsService.SetPreset("Rendering.LowPolyMeshes1", levels[0].ToString());
                _flagsService.SetPreset("Rendering.LowPolyMeshes2", levels[1].ToString());
                _flagsService.SetPreset("Rendering.LowPolyMeshes3", levels[2].ToString());
                _flagsService.SetPreset("Rendering.LowPolyMeshes4", levels[3].ToString());

                OnPropertyChanged();
                OnPropertyChanged(nameof(LowPolyMeshesEnabled));
            }
        }

        public bool PauseVoxelizer
        {
            get => _flagsService.GetPreset("Rendering.PauseVoxerlizer") == "True";
            set
            {
                _flagsService.SetPreset("Rendering.PauseVoxerlizer", value ? "True" : null);
                OnPropertyChanged();
            }
        }

        public bool GraySky
        {
            get => _flagsService.GetPreset("Graphic.GraySky") == "True";
            set
            {
                _flagsService.SetPreset("Graphic.GraySky", value ? "True" : null);
                OnPropertyChanged();
            }
        }

        public bool UseFastFlagManager
        {
            get => _settingsService.UseFastFlagManager;
            set
            {
                _settingsService.UseFastFlagManager = value;
                OnPropertyChanged();
            }
        }

        public IReadOnlyDictionary<MSAAMode, string?> MSAALevels => FastFlagManager.MSAAModes;

        public MSAAMode SelectedMSAALevel
        {
            get => MSAALevels.FirstOrDefault(x => x.Value == _flagsService.GetPreset("Rendering.MSAA1")).Key;
            set
            {
                _flagsService.SetPreset("Rendering.MSAA1", MSAALevels[value]);
                OnPropertyChanged();
            }
        }

        public IReadOnlyDictionary<RenderingMode, string> RenderingModes => FastFlagManager.RenderingModes;

        public RenderingMode SelectedRenderingMode
        {
            get => App.FastFlags?.GetPresetEnum(RenderingModes, "Rendering.Mode", "True") ?? RenderingMode.Default;
            set
            {
                RenderingMode[] DisableD3D11 = new RenderingMode[]
                {
                    RenderingMode.Vulkan,
                    RenderingMode.OpenGL,
                };

                App.FastFlags?.SetPresetEnum("Rendering.Mode", value.ToString(), "True");
                _flagsService.SetPreset("Rendering.Mode.DisableD3D11", DisableD3D11.Contains(value) ? "True" : null);
                OnPropertyChanged();
            }
        }

        public bool FixDisplayScaling
        {
            get => _flagsService.GetPreset("Rendering.DisableScaling") == "True";
            set
            {
                _flagsService.SetPreset("Rendering.DisableScaling", value ? "True" : null);
                OnPropertyChanged();
            }
        }

        public IReadOnlyDictionary<QualityLevel, string?> QualityLevels => FastFlagManager.QualityLevels;

        public QualityLevel SelectedQualityLevel
        {
            get => FastFlagManager.QualityLevels.FirstOrDefault(x => x.Value == _flagsService.GetPreset("Rendering.FrmQuality")).Key;
            set
            {
                if (value == QualityLevel.Disabled)
                {
                    _flagsService.SetPreset("Rendering.FrmQuality", null);
                }
                else
                {
                    _flagsService.SetPreset("Rendering.FrmQuality", FastFlagManager.QualityLevels[value]);
                }
                OnPropertyChanged();
            }
        }

        public bool GetFlagAsBool(string flagKey, string falseValue = "False")
        {
            return _flagsService.GetPreset(flagKey) != falseValue;
        }

        public void SetFlagFromBool(string flagKey, bool value, string falseValue = "False")
        {
            _flagsService.SetPreset(flagKey, value ? null : falseValue);
        }

        public bool ResetConfiguration
        {
            get => _preResetFlags is not null;
            set
            {
                if (value)
                {
                    _preResetFlags = new(_flagsService.GetAllPresets());
                    _flagsService.SetAllPresets(new Dictionary<string, object>());
                }
                else
                {
                    if (_preResetFlags != null)
                    {
                        _flagsService.SetAllPresets(_preResetFlags);
                        _preResetFlags = null;
                    }
                }

                RequestPageReloadEvent?.Invoke(this, EventArgs.Empty);
                OnPropertyChanged();
            }
        }
    }

    internal class DefaultFastFlagsService : IFastFlagsService
    {
        public string? GetPreset(string key) => App.FastFlags?.GetPreset(key);

        public void SetPreset(string key, string? value) => App.FastFlags?.SetPreset(key, value);

        public object? GetPresetEnum(IReadOnlyDictionary<object, string> enumMap, string key, string defaultValue)
        {
            return App.FastFlags?.GetPreset(key);
        }

        public void SetPresetEnum(string key, string value, string defaultValue)
            => App.FastFlags?.SetPreset(key, value);

        public IReadOnlyDictionary<string, object> GetAllPresets() => App.FastFlags?.Prop ?? new Dictionary<string, object>();

        public void SetAllPresets(IReadOnlyDictionary<string, object> presets)
        {
            if (App.FastFlags != null)
            {
                App.FastFlags.Prop.Clear();
                foreach (var kvp in presets)
                {
                    App.FastFlags.Prop[kvp.Key] = kvp.Value;
                }
            }
        }
    }

    internal class DefaultSettingsService : ISettingsService
    {
        public bool UseFastFlagManager
        {
            get => App.Settings.Prop.UseFastFlagManager;
            set => App.Settings.Prop.UseFastFlagManager = value;
        }
    }

    internal class DefaultDialogService : IDialogService
    {
        public Task OpenFastFlagEditorAsync()
        {
            return Task.CompletedTask;
        }
    }
}