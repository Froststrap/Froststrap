using System.Text.Json.Nodes;

namespace Froststrap.Models
{
    internal sealed class ModGeneratorInfo
    {
        public string FroststrapVersion { get; set; } = "";
        public string RobloxVersion { get; set; } = "";
        public string RobloxVersionHash { get; set; } = "";
        public JsonNode? ColorsUsed { get; set; }
    }
}