using CommonLib.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
        private readonly BlockPos _startPos;
        private readonly BlockSchematicStructure _schematic;
        private readonly StructureRandomizer _system;

        private readonly LCGRandom _random;
        private readonly string _currentWood;
        private readonly string _currentClay;

        private bool _skip = false;

        public StructureRandomizerInstance(ICoreServerAPI api, BlockPos startPos, BlockSchematicStructure schematic, StructureRandomizer system)
        {
            _startPos = startPos;
            _schematic = schematic;
            _system = system;

            _random = new LCGRandom(api.World.Seed);
            _random.InitPositionSeed(startPos.X, startPos.Z);
            _currentWood = _random.GetItem(system.Woods);
            _currentClay = _random.GetItem(system.Clays);
        }

        public Dictionary<int, Dictionary<int, int>> GetNewReplaceBlocks(Dictionary<int, Dictionary<int, int>> oldReplaceBlocks, IBlockAccessor blockAccessor, IWorldAccessor world, int? rockBlockId)
        {
            if (oldReplaceBlocks == null)
                return oldReplaceBlocks!;

            if (!_schematic.BlockCodes.Any(x => x.Value.Domain == Constants.ModId)) // Skip generator
            {
                _skip = true;
                return oldReplaceBlocks;
            }

            var newReplaceBlocks = new Dictionary<int, Dictionary<int, int>>();
            lock (oldReplaceBlocks)
            {
                foreach (var inner in oldReplaceBlocks)
                {
                    var innerCopy = new Dictionary<int, int>();
                    foreach (var value in inner.Value)
                    {
                        innerCopy.Add(value.Key, value.Value);
                    }
                    newReplaceBlocks.Add(inner.Key, innerCopy);
                }
            }

            if (!rockBlockId.HasValue)
            {
                var blockPos = new BlockPos(_schematic.SizeX / 2 + _startPos.X, _startPos.Y, _schematic.SizeZ / 2 + _startPos.Z, _startPos.dimension);
                var mapChunkAtBlockPos = blockAccessor.GetMapChunkAtBlockPos(blockPos);
                rockBlockId ??= mapChunkAtBlockPos.TopRockIdMap[blockPos.Z % 32 * 32 + blockPos.X % 32];
            }

            foreach (var (_, code) in _schematic.BlockCodes)
            {
                var blockId = world.GetBlock(code).Id;
                var newBlockId = Next(blockId, world);
                if (newBlockId != blockId)
                {
                    if (newReplaceBlocks.TryGetValue(blockId, out var value))
                    {
                        value[rockBlockId.Value] = newBlockId;
                    }
                    else
                    {
                        newReplaceBlocks.Add(blockId, new() { [rockBlockId.Value] = newBlockId });
                    }
                }
            }

            return newReplaceBlocks;
        }

        private int Next(int blockId, IWorldAccessor world)
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

        public void AfterPlace(IBlockAccessor blockAccessor)
        {
            if (_skip == true) return;

            var lantern = _random.GetItem(_system.LanternMaterials);
            var tempPos = new BlockPos(_startPos.dimension);
            for (int x = 0; x < _schematic.SizeX; x++)
            {
                for (int y = 0; y < _schematic.SizeY; y++)
                {
                    for (int z = 0; z < _schematic.SizeZ; z++)
                    {
                        var block = _schematic.blocksByPos[x, y, z];
                        if (block is BlockLantern)
                        {
                            tempPos.Set(_startPos.X + x, _startPos.Y + y, _startPos.Z + z);
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

        public Dictionary<AssetLocation, string[]> ResolvedReplaceBlocks { get; } = [];
        public string[] Woods { get; }
        public string[] Clays { get; }
        public List<AssetLocation> LightBlocks { get; } = [];
        public string[] LanternMaterials => _props.LanternMaterials;

        public StructureRandomizer(ICoreServerAPI api)
        {
            _props = api.Assets.Get($"{Constants.ModId}:worldgen/randomizer.json").ToObject<StructureRandomizerProperties>();

            var woodProps = api.Assets.Get("game:worldproperties/block/wood.json").ToObject<WoodWorldProperty>();
            Woods = woodProps.Variants.Select(v => v.Code.ToShortString()).AddItem("aged").ToArray();
            Clays = ["black", "brown", "cream", "fire", "gray", "orange", "red", "tan"];

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

                    ResolvedReplaceBlocks.Add(block.Code, suitableCodes.ToArray());
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
