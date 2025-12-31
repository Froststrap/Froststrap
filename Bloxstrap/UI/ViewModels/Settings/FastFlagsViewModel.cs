using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Bloxstrap.Enums.FlagPresets;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class FastFlagsViewModel : NotifyPropertyChangedViewModel
    {
        private Dictionary<string, object>? _preResetFlags;

        public event EventHandler? RequestPageReloadEvent;

        public event EventHandler? OpenFlagEditorEvent;

        private void OpenFastFlagEditor() => OpenFlagEditorEvent?.Invoke(this, EventArgs.Empty);

        public ICommand OpenFastFlagEditorCommand => new RelayCommand(OpenFastFlagEditor);

        public bool RemoveGrass
        {
            get => App.FastFlags?.GetPreset("Rendering.RemoveGrass1") == "0";
            set
            {
                App.FastFlags.SetPreset("Rendering.RemoveGrass1", value ? "0" : null);
                App.FastFlags.SetPreset("Rendering.RemoveGrass2", value ? "0" : null);
                App.FastFlags.SetPreset("Rendering.RemoveGrass3", value ? "0" : null);
            }
        }

        public bool LowPolyMeshesEnabled
        {
            get => App.FastFlags.GetPreset("Rendering.LowPolyMeshes1") != null;
            set
            {
                if (value)
                {
                    LowPolyMeshesLevel = 5;
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.LowPolyMeshes1", null);
                    App.FastFlags.SetPreset("Rendering.LowPolyMeshes2", null);
                    App.FastFlags.SetPreset("Rendering.LowPolyMeshes3", null);
                    App.FastFlags.SetPreset("Rendering.LowPolyMeshes4", null);
                }
                OnPropertyChanged(nameof(LowPolyMeshesEnabled));
            }
        }

        public int LowPolyMeshesLevel
        {
            get
            {
                if (int.TryParse(App.FastFlags.GetPreset("Rendering.LowPolyMeshes1"), out var storedValue))
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

                App.FastFlags.SetPreset("Rendering.LowPolyMeshes1", levels[0].ToString());
                App.FastFlags.SetPreset("Rendering.LowPolyMeshes2", levels[1].ToString());
                App.FastFlags.SetPreset("Rendering.LowPolyMeshes3", levels[2].ToString());
                App.FastFlags.SetPreset("Rendering.LowPolyMeshes4", levels[3].ToString());

                OnPropertyChanged(nameof(LowPolyMeshesLevel));
                OnPropertyChanged(nameof(LowPolyMeshesEnabled));
            }
        }

        public bool PauseVoxelizer
        {
            get => App.FastFlags.GetPreset("Rendering.PauseVoxerlizer") == "True";
            set => App.FastFlags.SetPreset("Rendering.PauseVoxerlizer", value ? "True" : null);
        }

        public bool GraySky
        {
            get => App.FastFlags.GetPreset("Graphic.GraySky") == "True";
            set => App.FastFlags.SetPreset("Graphic.GraySky", value ? "True" : null);
        }

        public bool UseFastFlagManager
        {
            get => App.Settings.Prop.UseFastFlagManager;
            set => App.Settings.Prop.UseFastFlagManager = value;
        }

        public IReadOnlyDictionary<MSAAMode, string?> MSAALevels => FastFlagManager.MSAAModes;

        public MSAAMode SelectedMSAALevel
        {
            get => MSAALevels.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.MSAA1")).Key;
            set => App.FastFlags.SetPreset("Rendering.MSAA1", MSAALevels[value]);
        }

        public IReadOnlyDictionary<TextureQuality, string?> TextureQualities => FastFlagManager.TextureQualityLevels;

        public TextureQuality SelectedTextureQuality
        {
            get => TextureQualities.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.TextureQuality.Level")).Key;
            set
            {
                if (value == TextureQuality.Default)
                {
                    App.FastFlags.SetPreset("Rendering.TextureQuality", null);
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.TextureQuality.OverrideEnabled", "True");
                    App.FastFlags.SetPreset("Rendering.TextureQuality.Level", TextureQualities[value]);
                }
            }
        }

        public IReadOnlyDictionary<RenderingMode, string> RenderingModes => FastFlagManager.RenderingModes;

        public RenderingMode SelectedRenderingMode
        {
            get => App.FastFlags.GetPresetEnum(RenderingModes, "Rendering.Mode", "True");
            set
            {
                RenderingMode[] DisableD3D11 = new RenderingMode[]
                {
                    RenderingMode.Vulkan,
                    RenderingMode.OpenGL,
                };

                App.FastFlags.SetPresetEnum("Rendering.Mode", value.ToString(), "True");
                App.FastFlags.SetPreset("Rendering.Mode.DisableD3D11", DisableD3D11.Contains(value) ? "True" : null);
            }
        }

        public bool FixDisplayScaling
        {
            get => App.FastFlags.GetPreset("Rendering.DisableScaling") == "True";
            set => App.FastFlags.SetPreset("Rendering.DisableScaling", value ? "True" : null);
        }

        public IReadOnlyDictionary<QualityLevel, string?> QualityLevels => FastFlagManager.QualityLevels;

        public QualityLevel SelectedQualityLevel
        {
            get => FastFlagManager.QualityLevels.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.FrmQuality")).Key;
            set
            {
                if (value == QualityLevel.Disabled)
                {
                    App.FastFlags.SetPreset("Rendering.FrmQuality", null);
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.FrmQuality", FastFlagManager.QualityLevels[value]);
                }
            }
        }

        public bool GetFlagAsBool(string flagKey, string falseValue = "False")
        {
            return App.FastFlags.GetPreset(flagKey) != falseValue;
        }

        public void SetFlagFromBool(string flagKey, bool value, string falseValue = "False")
        {
            App.FastFlags.SetPreset(flagKey, value ? null : falseValue);
        }

        public bool ResetConfiguration
        {
            get => _preResetFlags is not null;
            set
            {
                if (value)
                {
                    _preResetFlags = new(App.FastFlags.Prop);
                    App.FastFlags.Prop.Clear();
                }
                else
                {
                    App.FastFlags.Prop = _preResetFlags!;
                    _preResetFlags = null;
                }

                RequestPageReloadEvent?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}