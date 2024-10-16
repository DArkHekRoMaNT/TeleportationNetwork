using CommonLib.Extensions;
using HarmonyLib;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace TeleportationNetwork.Generation.v2
{
    public class StructureRandomizerInstance
    {
        private readonly LCGRandom _random;
        private readonly StructureRandomizer _system;

        private readonly string _currentWood;
        private readonly string _currentClay;

        public StructureRandomizerInstance(ICoreServerAPI api, BlockPos pos, StructureRandomizer system)
        {
            _system = system;

            _random = new LCGRandom();
            _random.SetWorldSeed(api.World.Seed);
            _random.InitPositionSeed(pos.X, pos.Z);

            _currentWood = _random.GetItem(system.Woods);
            _currentClay = _random.GetItem(system.Clays);
        }

        public int Next(int blockId, IWorldAccessor world)
        {
            var code = world.GetBlock(blockId).Code;

            if (code == null)
                return blockId;

            if (!Core.Config.BiomlessTeleports && _system.ResolvedReplaceBlocks.TryGetValue(code, out var alternatives))
            {
                var selected = _random.GetItem(alternatives)
                    .Replace("{wood}", _currentWood)
                    .Replace("{clay}", _currentClay);
                code = new AssetLocation(selected);
            }

            if (Core.Config.DarknessMode && _system.LightBlocks.Contains(code))
            {
                code = new AssetLocation("air");
            }

            var newBlock = world.GetBlock(code);
            if (newBlock != null && !newBlock.IsMissing)
            {
                return newBlock.Id;
            }
            return blockId;
        }

        public void AfterPlace(IBlockAccessor blockAccessor, BlockPos startPos, BlockSchematicStructure structure)
        {
            var lantern = _random.GetItem(_system.LanternMaterials);
            var tempPos = new BlockPos(startPos.dimension);
            for (int x = 0; x < structure.SizeX; x++)
            {
                for (int y = 0; y < structure.SizeY; y++)
                {
                    for (int z = 0; z < structure.SizeZ; z++)
                    {
                        var block = structure.blocksByPos[x, y, z];
                        if (block is BlockLantern)
                        {
                            tempPos.Set(startPos.X + x, startPos.Y + y, startPos.Z + z);
                            var be = blockAccessor.GetBlockEntity(tempPos);
                            if (be is BELantern lbe)
                            {
                                lbe.material = lantern;
                            }
                        }
                    }
                }
            }
        }

    }

    public class StructureRandomizer
    {
        private readonly StructureRandomizerProperties _props;

        public ConcurrentDictionary<AssetLocation, string[]> ResolvedReplaceBlocks { get; } = [];
        public string[] Woods { get; }
        public string[] Clays { get; }
        public List<AssetLocation> LightBlocks { get; } = [];
        public string[] LanternMaterials => _props.LanternMaterials;

        public StructureRandomizer(ICoreServerAPI api)
        {
            _props = api.Assets.Get($"{Constants.ModId}:worldgen/randomizer.json").ToObject<StructureRandomizerProperties>();

            var woodProps = api.Assets.Get("game:worldproperties/block/wood.json").ToObject<WoodWorldProperty>();
            Woods = woodProps.Variants.Select(v => v.Code.ToShortString()).AddItem("aged").ToArray();
            Clays = ["fire", "black", "brown", "cream", "gray", "orange", "red", "tan", "clinker"];

            foreach (var lightBlock in _props.LightBlocks)
            {
                var blocks = api.World.SearchBlocks((AssetLocation)lightBlock);
                LightBlocks.AddRange(blocks.Select(b => b.Code));
            }

            ResolveBlocks(api);
        }

        private void ResolveBlocks(ICoreServerAPI api)
        {
            foreach (var (codeStr, pattern) in _props.ReplaceBlocks)
            {
                var code = (AssetLocation)codeStr;
                foreach (var block in api.World.SearchBlocks(code))
                {
                    var currentPattern = pattern;
                    var suitableCodes = new List<string>();

                    if (pattern.Contains('*'))
                    {
                        var value = WildcardUtil.GetWildcardValue(code, block.Code);
                        currentPattern = pattern.Replace("*", value);
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

                    ResolvedReplaceBlocks.TryAdd(block.Code, suitableCodes.ToArray());
                }
            }
        }

        public static StructureRandomizerInstance GetRandomizer(IWorldAccessor world, BlockPos pos)
        {
            var randomizer = world.Api.ModLoader.GetModSystem<WorldGenSystem>().Randomizer;
            return new StructureRandomizerInstance((ICoreServerAPI)world.Api, pos, randomizer);
        }
    }
}
