namespace Froststrap.Models.SettingTasks
{
    internal class EmojiModPresetTask : EnumBaseTask<EmojiType>
    {
        private static string FilePath => Path.Combine(Paths.Modifications, "content", "fonts", "TwemojiMozilla.ttf");

        private static IEnumerable<KeyValuePair<EmojiType, string>>? QueryCurrentValue()
        {
            if (!File.Exists(FilePath))
                return null;

            using var fileStream = File.OpenRead(FilePath);
            string hash = SHA256Hash.Stringify(App.SHA256Provider.ComputeHash(fileStream));

            return EmojiTypeEx.Hashes.Where(x => x.Value == hash);
        }

        public EmojiModPresetTask() : base("ModPreset", "EmojiFont")
        {
            var query = QueryCurrentValue();

            if (query is not null)
                OriginalState = query.FirstOrDefault().Key;
        }

        public override async void Execute()
        {
            var query = QueryCurrentValue();

            if (NewState != EmojiType.Default)
            {
                var first = query?.FirstOrDefault();
                if (first?.Key != NewState)
                {
                    try
                    {
                        var response = await App.HttpClient.GetAsync(new Uri(NewState.GetUrl()));
                        response.EnsureSuccessStatusCode();

                        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                        await using var fileStream = new FileStream(FilePath, FileMode.Create);
                        await response.Content.CopyToAsync(fileStream);

                        OriginalState = NewState;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error("Unhandled exception: ", ex);
                        await Frontend.ShowConnectivityDialog(
                            string.Format(CultureInfo.CurrentCulture, Strings.Dialog_Connectivity_UnableToConnect, "GitHub"),
                            $"{Strings.Menu_PresetMods_Presets_EmojiType_Error}\n\n{Strings.Dialog_Connectivity_TryAgainLater}",
                            MessageBoxImage.Warning,
                            ex
                        );
                    }
                }
            }
            else if (query is not null && query.Any())
            {
                Filesystem.AssertReadOnly(FilePath);
                File.Delete(FilePath);

                OriginalState = NewState;
            }
        }
    }
}
