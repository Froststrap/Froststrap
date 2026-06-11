namespace Froststrap.Models.SettingTasks
{
    public class FontModPresetTask : StringBaseTask
    {
        private const string CustomFontAssetId = "rbxasset://fonts/CustomFont.ttf";
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
        private static string CustomFontFamiliesDirectory => Path.Combine(Paths.Modifications, "content", "fonts", "families");
        private static readonly string[] CustomFontFamilyFiles =
        [
            "AccanthisADFStd.json",
            "AmaticSC.json",
            "Arimo.json",
            "Balthazar.json",
            "Bangers.json",
            "BuilderExtended.json",
            "BuilderMono.json",
            "BuilderSans.json",
            "ComicNeueAngular.json",
            "Creepster.json",
            "DenkOne.json",
            "Fondamento.json",
            "FredokaOne.json",
            "GrenzeGotisch.json",
            "Guru.json",
            "HighwayGothic.json",
            "Inconsolata.json",
            "IndieFlower.json",
            "JosefinSans.json",
            "Jura.json",
            "Kalam.json",
            "LegacyArial.json",
            "LegacyArimo.json",
            "LuckiestGuy.json",
            "Merriweather.json",
            "Michroma.json",
            "Montserrat.json",
            "Nunito.json",
            "Oswald.json",
            "PatrickHand.json",
            "PermanentMarker.json",
            "PressStart2P.json",
            "Roboto.json",
            "RobotoCondensed.json",
            "RobotoMono.json",
            "RomanAntique.json",
            "Sarpanch.json",
            "SourceSansPro.json",
            "SpecialElite.json",
            "TitilliumWeb.json",
            "Ubuntu.json",
            "Zekton.json"
        ];

        private static readonly FontFace[] CustomFontFaces =
        [
            new() { Name = "Thin", Weight = 100, Style = "normal", AssetId = CustomFontAssetId },
            new() { Name = "Light", Weight = 300, Style = "normal", AssetId = CustomFontAssetId },
            new() { Name = "Regular", Weight = 400, Style = "normal", AssetId = CustomFontAssetId },
            new() { Name = "Medium", Weight = 500, Style = "normal", AssetId = CustomFontAssetId },
            new() { Name = "Semi Bold", Weight = 600, Style = "normal", AssetId = CustomFontAssetId },
            new() { Name = "Bold", Weight = 700, Style = "normal", AssetId = CustomFontAssetId },
            new() { Name = "Extra Bold", Weight = 800, Style = "normal", AssetId = CustomFontAssetId }
        ];

        public static string? GetFileHash()
        {
            if (!File.Exists(Paths.CustomFont))
                return null;

            using var fileStream = File.OpenRead(Paths.CustomFont);
            return MD5Hash.Stringify(App.MD5Provider.ComputeHash(fileStream));
        }

        public FontModPresetTask() : base("ModPreset", "TextFont")
        {
            if (File.Exists(Paths.CustomFont))
                OriginalState = Paths.CustomFont;
        }

        public override void Execute()
        {
            if (!String.IsNullOrEmpty(NewState))
            {
                if (String.Compare(NewState, Paths.CustomFont, StringComparison.InvariantCultureIgnoreCase) != 0 && File.Exists(NewState))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Paths.CustomFont)!);

                    Filesystem.AssertReadOnly(Paths.CustomFont);
                    File.Copy(NewState, Paths.CustomFont, true);
                }

                EnsureCustomFontFamilies();
            }
            else if (File.Exists(Paths.CustomFont))
            {
                Filesystem.AssertReadOnly(Paths.CustomFont);
                File.Delete(Paths.CustomFont);
            }

            if (String.IsNullOrEmpty(NewState))
                CleanupCustomFontFamilies();

            OriginalState = NewState;
        }

        private static void EnsureCustomFontFamilies()
        {
            Directory.CreateDirectory(CustomFontFamiliesDirectory);

            foreach (string familyFile in CustomFontFamilyFiles)
            {
                string targetPath = Path.Combine(CustomFontFamiliesDirectory, familyFile);
                var fontFamily = new FontFamily
                {
                    Name = GetFontFamilyNameFromFilename(familyFile),
                    Faces = CustomFontFaces
                };

                Filesystem.AssertReadOnly(targetPath);
                File.WriteAllText(
                    targetPath,
                    JsonSerializer.Serialize(fontFamily, SerializerOptions)
                );
            }
        }

        private static void CleanupCustomFontFamilies()
        {
            if (!Directory.Exists(CustomFontFamiliesDirectory))
                return;

            foreach (string jsonPath in Directory.GetFiles(CustomFontFamiliesDirectory, "*.json"))
            {
                if (!IsCustomFontFamilyOverride(jsonPath))
                    continue;

                Filesystem.AssertReadOnly(jsonPath);
                File.Delete(jsonPath);
            }

            if (!Directory.EnumerateFileSystemEntries(CustomFontFamiliesDirectory).Any())
                Directory.Delete(CustomFontFamiliesDirectory);
        }

        private static bool IsCustomFontFamilyOverride(string filePath)
        {
            string contents = File.ReadAllText(filePath);
            return contents.Contains(CustomFontAssetId, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetFontFamilyNameFromFilename(string familyFile)
        {
            string baseName = Path.GetFileNameWithoutExtension(familyFile);
            string withWordBoundaries = Regex.Replace(baseName, "(?<=[A-Z])([A-Z][a-z])", " $1");
            return Regex.Replace(withWordBoundaries, "(?<=[a-z0-9])([A-Z])", " $1");
        }
    }
}
