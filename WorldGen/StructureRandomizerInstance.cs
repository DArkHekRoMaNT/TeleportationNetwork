using CommonLib.Extensions;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace TeleportationNetwork.WorldGen
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
                var block = world.GetBlock(code);
                if (block == null)
                {
                    continue;
                }

                var blockId = block.Id;
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
                    .Replace("{clay}", _currentClay)
                    .Replace("{painting}", _random.GetItem(_system.Paintings));
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
}
