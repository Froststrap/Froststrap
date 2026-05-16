using Froststrap.Enums.FlagPresets;

namespace Froststrap
{
    public class FastFlagManager : JsonManager<Dictionary<string, object>>
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private static readonly JsonSerializerOptions _jsonReadOptions = new() { ReadCommentHandling = JsonCommentHandling.Skip };

        private Dictionary<string, object> OriginalProp = [];

        public override string ClassName => nameof(FastFlagManager);
        public override string LOG_IDENT_CLASS => ClassName;
        public override string ProfilesLocation => Path.Combine(Paths.Base, "Profiles");
        public override string FileName => "ClientAppSettings.json";
        public override string FileLocation => Path.Combine(Paths.PresetModifications, "ClientSettings", FileName);

        public bool Changed => !OriginalProp.SequenceEqual(Prop);

        public static readonly IReadOnlyDictionary<string, string> PresetFlags = new Dictionary<string, string>
        {
            // Preset Flags
            { "Rendering.ManualFullscreen", "FFlagHandleAltEnterFullscreenManually" },

            // Pause Voxelizer
            { "Rendering.PauseVoxerlizer", "DFFlagDebugPauseVoxelizer" },

            // DPI Scaling
            { "Rendering.DisableScaling", "DFFlagDisableDPIScale" },

            // Texture Quality Override
            { "Rendering.TextureQuality.OverrideEnabled", "DFFlagTextureQualityOverrideEnabled" },
            { "Rendering.TextureQuality.Level", "DFIntTextureQualityOverride" },
            { "Rendering.FrmQuality", "DFIntDebugFRMQualityLevelOverride" },

            // Low Poly Meshes
            { "Rendering.LowPolyMeshes1", "DFIntCSGLevelOfDetailSwitchingDistance" },
            { "Rendering.LowPolyMeshes2", "DFIntCSGLevelOfDetailSwitchingDistanceL12" },
            { "Rendering.LowPolyMeshes3", "DFIntCSGLevelOfDetailSwitchingDistanceL23" },
            { "Rendering.LowPolyMeshes4", "DFIntCSGLevelOfDetailSwitchingDistanceL34" },

            // Rendering Modes
            { "Rendering.Mode.D3D11", "FFlagDebugGraphicsPreferD3D11" },
            { "Rendering.Mode.Vulkan", "FFlagDebugGraphicsPreferVulkan" },
            { "Rendering.Mode.OpenGL", "FFlagDebugGraphicsPreferOpenGL" },

            // Skys
            { "Graphic.GraySky", "FFlagDebugSkyGray" },

            // MSAA
            { "Rendering.MSAA1", "FIntDebugForceMSAASamples" },

            // Remove Grass
            { "Rendering.RemoveGrass1", "FIntFRMMinGrassDistance" },
            { "Rendering.RemoveGrass2", "FIntFRMMaxGrassDistance" },
            { "Rendering.RemoveGrass3", "FIntGrassMovementReducedMotionFactor" },
        };

        public static IReadOnlyDictionary<RenderingMode, string> RenderingModes => new Dictionary<RenderingMode, string>
        {
            { RenderingMode.Default, "None" },
            { RenderingMode.Vulkan, "Vulkan" },
            { RenderingMode.OpenGL, "OpenGL" },

        };

        public static IReadOnlyDictionary<TextureQuality, string?> TextureQualityLevels => new Dictionary<TextureQuality, string?>
        {
            { TextureQuality.Default, null },
            { TextureQuality.Medium, "0" },
            { TextureQuality.Low, "1" },
            { TextureQuality.Lowest, "2" },
        };

        public static IReadOnlyDictionary<MSAAMode, string?> MSAAModes => new Dictionary<MSAAMode, string?>
        {
            { MSAAMode.Default, null },
            { MSAAMode.x1, "1" },
            { MSAAMode.x2, "2" },
            { MSAAMode.x4, "4" }
        };

        public static IReadOnlyDictionary<QualityLevel, string?> QualityLevels => new Dictionary<QualityLevel, string?>
        {
            { QualityLevel.Disabled, null },
            { QualityLevel.Level1, "1" },
            { QualityLevel.Level2, "2" },
            { QualityLevel.Level3, "3" },
            { QualityLevel.Level4, "4" },
            { QualityLevel.Level5, "5" },
            { QualityLevel.Level6, "6" },
            { QualityLevel.Level7, "7" },
            { QualityLevel.Level8, "8" },
            { QualityLevel.Level9, "9" },
            { QualityLevel.Level10, "10" },
            { QualityLevel.Level11, "11" },
            { QualityLevel.Level12, "12" },
            { QualityLevel.Level13, "13" },
            { QualityLevel.Level14, "14" },
            { QualityLevel.Level15, "15" },
            { QualityLevel.Level16, "16" },
            { QualityLevel.Level17, "17" },
            { QualityLevel.Level18, "18" },
            { QualityLevel.Level19, "19" },
            { QualityLevel.Level20, "20" },
            { QualityLevel.Level21, "21" }
        };

        public bool suspendUndoSnapshot = false;

        public void SetValue(string key, object? value)
        {
            const string LOG_IDENT = "FastFlagManager::SetValue";

            if (!suspendUndoSnapshot)
                SaveUndoSnapshot();

            if (value is null)
            {
                if (Prop.ContainsKey(key))
                    App.Logger.WriteLine(LOG_IDENT, $"Deletion of '{key}' is pending");

                Prop.Remove(key);
            }
            else
            {
                if (Prop.TryGetValue(key, out object? existingValue))
                {
                    if (string.Equals(value.ToString(), existingValue?.ToString(), StringComparison.Ordinal))
                        return;

                    App.Logger.WriteLine(LOG_IDENT, $"Changing of '{key}' from '{existingValue}' to '{value}' is pending");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Setting of '{key}' to '{value}' is pending");
                }

                Prop[key] = value.ToString()!;
            }
        }

        public string? GetValue(string key)
        {
            if (Prop.TryGetValue(key, out object? value) && value is not null)
                return value.ToString();

            return null;
        }

        public void SetPreset(string prefix, object? value)
        {
            foreach (var pair in PresetFlags.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal)))
                SetValue(pair.Value, value);
        }

        public void SetPresetEnum(string prefix, string target, object? value)
        {
            foreach (var pair in PresetFlags.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal)))
            {
                if (pair.Key.StartsWith($"{prefix}.{target}", StringComparison.Ordinal))
                    SetValue(pair.Value, value);
                else
                    SetValue(pair.Value, null);
            }
        }

        public string? GetPreset(string name)
        {
            if (!PresetFlags.TryGetValue(name, out string? flagName))
            {
                App.Logger.WriteLine("FastFlagManager::GetPreset", $"Could not find preset {name}");
                Debug.Assert(false, $"Could not find preset {name}");
                return null;
            }

            return GetValue(flagName);
        }

        public T GetPresetEnum<T>(IReadOnlyDictionary<T, string> mapping, string prefix, string value) where T : Enum
        {
            foreach (var pair in mapping)
            {
                if (pair.Value == "None")
                    continue;

                if (GetPreset($"{prefix}.{pair.Value}") == value)
                    return pair.Key;
            }

            return mapping.First().Key;
        }

        public static bool IsPreset(string flag) => PresetFlags.Values.Any(v => string.Equals(v, flag, StringComparison.OrdinalIgnoreCase));

        public override void Save()
        {
            foreach (var key in Prop.Keys.ToList())
                Prop[key] = Prop[key].ToString()!;

            base.Save();

            OriginalProp = new(Prop);

            if (OperatingSystem.IsLinux())
                SyncToSoberConfig();
        }

        [System.Runtime.Versioning.SupportedOSPlatform("linux")]
        private void SyncToSoberConfig()
        {
            const string LOG_IDENT = "FastFlagManager::SyncToSoberConfig";

            try
            {
                if (!App.SoberSettings.Loaded)
                {
                    App.SoberSettings.Load(alertFailure: false);
                }

                App.SoberSettings.Prop.FFlags ??= [];

                foreach (var kvp in Prop)
                {
                    string val = kvp.Value?.ToString() ?? "";

                    if (bool.TryParse(val, out bool boolResult))
                    {
                        App.SoberSettings.Prop.FFlags[kvp.Key] = boolResult;
                    }
                    else if (long.TryParse(val, out long longResult))
                    {
                        App.SoberSettings.Prop.FFlags[kvp.Key] = longResult;
                    }
                    else if (double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double doubleResult))
                    {
                        App.SoberSettings.Prop.FFlags[kvp.Key] = doubleResult;
                    }
                    else
                    {
                        App.SoberSettings.Prop.FFlags[kvp.Key] = val;
                    }
                }

                App.SoberSettings.Save();

                App.Logger.WriteLine(LOG_IDENT, $"Successfully synchronized and saved {App.SoberSettings.Prop.FFlags.Count} fflags to SoberSettings.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to sync fflags into App.SoberSettings configuration.");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public override bool Load(bool alertFailure = true)
        {
            bool result = base.Load(alertFailure);
            OriginalProp = new(Prop);

            if (GetPreset("Rendering.ManualFullscreen") != "False")
                SetPreset("Rendering.ManualFullscreen", "False");

            return result;
        }

        public static async Task DeleteProfile(string profile)
        {
            try
            {
                string profilesDirectory = Path.Combine(Paths.Base, Paths.SavedFlagProfiles);

                if (!Directory.Exists(profilesDirectory))
                    Directory.CreateDirectory(profilesDirectory);

                if (string.IsNullOrEmpty(profile))
                    return;

                File.Delete(Path.Combine(profilesDirectory, profile));
            }
            catch (Exception ex)
            {
                await Frontend.ShowMessageBox(ex.Message, MessageBoxImage.Error);
            }
        }

        public IEnumerable<FastFlag> GetAllFlags()
        {
            foreach (var kvp in Prop)
            {
                yield return new FastFlag
                {
                    Name = kvp.Key,
                    Value = kvp.Value?.ToString() ?? "",
                    Preset = FluentIcons.Common.Symbol.Subtract
                };
            }
        }

        private readonly Stack<Dictionary<string, object>> undoStack = [];
        private readonly Stack<Dictionary<string, object>> redoStack = [];

        public void SaveUndoSnapshot()
        {
            if (undoStack.Count > 0 && DictionaryEquals(undoStack.Peek(), Prop))
                return;

            undoStack.Push(new Dictionary<string, object>(Prop));
            redoStack.Clear();
        }

        private static bool DictionaryEquals(Dictionary<string, object> a, Dictionary<string, object> b)
        {
            if (a.Count != b.Count)
                return false;

            foreach (var pair in a)
            {
                if (!b.TryGetValue(pair.Key, out var bValue))
                    return false;

                if (!Equals(pair.Value, bValue))
                    return false;
            }

            return true;
        }

        public void Undo()
        {
            if (undoStack.Count == 0)
                return;

            redoStack.Push(new Dictionary<string, object>(Prop));

            var previous = undoStack.Pop();

            Prop.Clear();
            foreach (var kvp in previous)
                Prop[kvp.Key] = kvp.Value;
        }

        public void Redo()
        {
            if (redoStack.Count == 0)
                return;

            undoStack.Push(new Dictionary<string, object>(Prop));

            var next = redoStack.Pop();

            Prop.Clear();
            foreach (var kvp in next)
                Prop[kvp.Key] = kvp.Value;
        }
    }
}
