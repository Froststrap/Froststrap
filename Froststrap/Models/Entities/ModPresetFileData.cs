namespace Froststrap.Models.Entities
{
    internal class ModPresetFileData
    {
        public string FilePath { get; private set; }

        public string FullFilePath => Path.Combine(Paths.Modifications, FilePath);

        public FileStream FileStream => File.OpenRead(FullFilePath);

        public string ResourceIdentifier { get; private set; }

        public Stream ResourceStream => Resource.GetStream(ResourceIdentifier);

        public byte[] ResourceHash { get; private set; }

        public ModPresetFileData(string contentPath, string resource)
        {
            //Why does linux do this bro, pisses me off
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
            ResourceHash = App.SHA256Provider.ComputeHash(stream);
        }

        public bool HashMatches()
        {
            if (!File.Exists(FullFilePath))
                return false;

            using var fileStream = FileStream;
            var fileHash = App.SHA256Provider.ComputeHash(fileStream);

            return fileHash.SequenceEqual(ResourceHash);
        }
    }
}
