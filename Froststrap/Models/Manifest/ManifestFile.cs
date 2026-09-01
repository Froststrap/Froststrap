namespace Froststrap.Models.Manifest
{
    internal class ManifestFile
    {
        public string Name { get; set; } = "";
        public string Signature { get; set; } = "";

        public override string ToString()
        {
            return $"[{Signature}] {Name}";
        }
    }
}
