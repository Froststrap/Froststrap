using System.Reflection;

namespace Froststrap.Models.SettingTasks
{
    public class ExtractIconsTask : BoolBaseTask
    {
        private static string IconPath => Path.Combine(Paths.Base, Strings.Paths_Icons);

        // List of embedded icon resource names to extract
        private static readonly string[] AllowedIconNames =
        [
            "Icon2008.ico",
            "Icon2011.ico",
            "Icon2017.ico",
            "Icon2019.ico",
            "Icon2022.ico",
            "Icon2025.ico",
            "IconFroststrap.ico",
            "IconEarly2015.ico",
            "IconLate2015.ico"
        ];

        public ExtractIconsTask() : base("ExtractIcons")
        {
            OriginalState = Directory.Exists(IconPath);
        }

        public override void Execute()
        {
            if (NewState)
            {
                Directory.CreateDirectory(IconPath);

                var assembly = Assembly.GetExecutingAssembly();
                var resourceNames = assembly.GetManifestResourceNames();

                foreach (string iconName in AllowedIconNames)
                {
                    string fullResourceName = $"Froststrap.Resources.{iconName}";

                    if (!resourceNames.Contains(fullResourceName))
                        continue;

                    using var stream = assembly.GetManifestResourceStream(fullResourceName)!;
                    using var memoryStream = new MemoryStream();
                    stream.CopyTo(memoryStream);

                    string filePath = Path.Combine(IconPath, iconName);
                    Filesystem.AssertReadOnly(filePath);
                    File.WriteAllBytes(filePath, memoryStream.ToArray());
                }
            }
            else if (Directory.Exists(IconPath))
            {
                Directory.Delete(IconPath, true);
            }

            OriginalState = NewState;
        }
    }
}