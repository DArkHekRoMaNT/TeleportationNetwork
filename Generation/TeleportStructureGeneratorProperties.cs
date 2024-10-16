using Newtonsoft.Json;
using System.Collections.Generic;

namespace TeleportationNetwork.Generation
{
    public class TeleportStructureGeneratorProperties
    {
        [JsonProperty, JsonRequired] public TeleportStructureProperties[] Structures { get; private set; } = null!;
        [JsonProperty, JsonRequired] public Dictionary<string, string> ReplaceBlocks { get; private set; } = null!;
        [JsonProperty, JsonRequired] public string[] ExcludeCodes { get; private set; } = null!;
        [JsonProperty, JsonRequired] public string[] LightBlocks { get; private set; } = null!;
        [JsonProperty, JsonRequired] public string[] LanternMaterials { get; private set; } = null!;
        [JsonProperty, JsonRequired] public string[] IgnoreRuin { get; private set; } = null!;
        [JsonProperty, JsonRequired] public string[] RuinPlants { get; private set; } = null!;
    }
}
