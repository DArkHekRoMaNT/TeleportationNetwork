using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace TeleportationNetwork.WorldGen
{
    public class StructureRandomizer
    {
        private readonly StructureRandomizerProperties _props;

        public Dictionary<AssetLocation, string[]> ResolvedReplaceBlocks { get; } = [];
        public string[] Woods { get; }
        public string[] Clays { get; }
        public string[] Paintings { get; }
        public List<AssetLocation> LightBlocks { get; } = [];
        public string[] LanternMaterials => _props.LanternMaterials;

        public StructureRandomizer(ICoreServerAPI api)
        {
            _props = api.Assets.Get($"{Constants.ModId}:worldgen/randomizer.json").ToObject<StructureRandomizerProperties>();

            var woodProps = api.Assets.Get("game:worldproperties/block/wood.json").ToObject<WoodWorldProperty>();
            Woods = woodProps.Variants.Select(v => v.Code.ToShortString()).AddItem("aged").ToArray();

            Clays = ["black", "brown", "cream", "fire", "gray", "orange", "red", "tan"];

            var paintingProps = api.Assets.Get("game:worldproperties/block/painting.json").ToObject<WoodWorldProperty>();
            Paintings = paintingProps.Variants.Select(v => v.Code.ToShortString()).ToArray();

            foreach (var lightBlock in _props.LightBlocks)
            {
                var blocks = api.World.SearchBlocks((AssetLocation)lightBlock);
                LightBlocks.AddRange(blocks.Select(b => b.Code));
            }

            ResolveBlocks(api);
        }

        private void ResolveBlocks(ICoreServerAPI api)
        {
            foreach (var (code, pattern) in _props.ReplaceBlocks)
            {
                void CheckBlocks(string code)
                {
                    foreach (var block in api.World.SearchBlocks((AssetLocation)code))
                    {
                        var currentPattern = pattern;
                        var suitableCodes = new List<string>();

                        if (currentPattern.Contains('*'))
                        {
                            var value = WildcardUtil.GetWildcardValue((AssetLocation)code, block.Code);
                            currentPattern = currentPattern.Replace("*", value);
                        }

                        if (currentPattern.Contains("{any}"))
                        {
                            var anyBlocks = api.World.SearchBlocks(new AssetLocation(currentPattern.Replace("{any}", "*")));
                            suitableCodes.AddRange(anyBlocks.Select(b => b.Code.ToString()));
                        }
                        else
                        {
                            suitableCodes.Add((AssetLocation)currentPattern);
                        }

                        foreach (var excludeCode in _props.ExcludeCodes)
                        {
                            suitableCodes.Remove((AssetLocation)excludeCode);
                        }

                        ResolvedReplaceBlocks.Add(block.Code, suitableCodes.ToArray());
                    }
                }

                if (code.Contains("{painting}"))
                {
                    foreach(var paiting in Paintings)
                    {
                        CheckBlocks(code.Replace("{painting}", paiting));
                    }
                }
                else
                {
                    CheckBlocks(code);
                }
            }
        }

        public static StructureRandomizerInstance GetRandomizer(IWorldAccessor world, BlockPos pos, BlockSchematicStructure schematic)
        {
            var randomizer = world.Api.ModLoader.GetModSystem<WorldGenSystem>().Randomizer;
            return new StructureRandomizerInstance((ICoreServerAPI)world.Api, pos, schematic, randomizer);
        }
    }
}
