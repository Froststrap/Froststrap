namespace Froststrap.Models.Entities
{
    internal class ModPresetFileData
    {
        public string FilePath { get; private set; }

        public string FullFilePath => Path.Combine(Paths.Modifications, FilePath);

        public FileStream FileStream => File.OpenRead(FullFilePath);

        public string ResourceIdentifier { get; private set; }

        public Stream ResourceStream => Resource.GetStream(ResourceIdentifier);

        public string ResourceHash { get; private set; }

        public ModPresetFileData(string contentPath, string resource)
        {
            if (OperatingSystem.IsLinux())
            {
                var parts = contentPath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
                FilePath = Path.Combine(parts);
            }
            else
            {
                FilePath = contentPath;
            }
            ResourceIdentifier = resource;
            using var stream = ResourceStream;
            ResourceHash = FastHash.FromStream(stream);
        }

        public bool HashMatches()
        {
            if (!File.Exists(FullFilePath))
                return false;

            using var fileStream = FileStream;
            var fileHash = FastHash.FromStream(fileStream);

            return fileHash == ResourceHash;
        }
    }
}