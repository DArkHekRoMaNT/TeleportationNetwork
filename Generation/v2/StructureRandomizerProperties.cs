using Newtonsoft.Json;
using System.Collections.Generic;

namespace TeleportationNetwork.Generation.v2
{
    [JsonObject]
    public class StructureRandomizerProperties
    {
        [JsonProperty] public Dictionary<string, string> ReplaceBlocks { get; init; } = [];
        [JsonProperty] public string[] ExcludeCodes { get; init; } = [];
        [JsonProperty] public string[] LightBlocks { get; init; } = [];
        [JsonProperty] public string[] LanternMaterials { get; init; } = [];
    }
}
