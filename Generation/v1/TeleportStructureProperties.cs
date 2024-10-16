using Newtonsoft.Json;

namespace TeleportationNetwork.Generation.v1
{
    public class TeleportStructureProperties
    {
        [JsonProperty, JsonRequired] public string Code { get; private set; } = null!;
        [JsonProperty, JsonRequired] public string[] Schematics { get; private set; } = null!;
        [JsonProperty] public string[] PillarSchematics { get; private set; } = [];
        [JsonProperty] public string[] BaseSchematics { get; private set; } = [];

        [JsonProperty] public bool Ruin { get; private set; } = false;
        [JsonProperty] public bool Special { get; private set; } = false;

        [JsonProperty] public bool BuildProtected { get; private set; } = false;
        [JsonProperty] public string? BuildProtectionName { get; private set; }
        [JsonProperty] public string? BuildProtectionDesc { get; private set; }

        [JsonProperty] public float Chance { get; private set; } = 0.05f;
        [JsonProperty] public int OffsetY { get; private set; } = 0;

        /// <summary>
        /// Place on the sea bottom only
        /// </summary>
        [JsonProperty] public bool PillarAlwaysTop { get; private set; } = false;
        [JsonProperty] public bool Underwater { get; private set; } = false;
        [JsonProperty] public int MaxDepth { get; private set; } = 15;
        [JsonProperty] public int MinDepth { get; private set; } = 0;
    }
}
