using CommonLib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class StructureBlockResolver
    {
        private static string[] Woods => new string[]
        {
            "aged",
            "agedebony",
            "birch",
            "oak",
            "maple",
            "pine",
            "acacia",
            "kapok",
            "baldcypress",
            "larch",
            "redwood",
            "ebony",
            "walnut",
            "purpleheart"
        };

        private static string[] Shingles => new string[]
        {
            "blue",
            "brown",
            "fire",
            "red"
        };

        private static string[] Paintings => new string[]
        {
            "howl",
            "elk",
            "underwater",
            "prey",
            "forestdawn",
            "fishandtherain",
            "bogfort",
            "castleruin",
            "cow",
            "hunterintheforest",
            "seraph",
            "sleepingwolf",
            "sunkenruin",
            "traveler",
            "oldvillage",
            "lastday"
        };

        private static string[] Vessels => new string[]
        {
            "ashforest",
            "chthonic",
            "copper",
            "earthen",
            "rain",
            "burned",
            "cowrie",
            "rime",
            "oxblood",
            "loam",
            "undergrowth",
            "beehive",
            "harvest",
            "honeydew",
            "rutile",
            "seasalt",
            "springflowers",
            "volcanic"
        };

        private static string[] Lanterns => new string[]
        {
            "copper",
            "brass",
            "blackbronze",
            "bismuth",
            "tinbronze",
            "bismuthbronze",
            "iron",
            "molybdochalkos",
            "silver",
            "gold",
            "steel",
            "meteoriciron"
        };

        private static string[] Chairs => new string[]
        {
            "plain",
            "blue",
            "red",
            "yellow",
            "purple",
            "brown",
            "green",
            "orange",
            "black",
            "gray",
            "pink",
            "white"
        };

        private readonly BlockPos _tmpPos = new();
        private readonly AssetLocation[] _blockCodes;
        private readonly AssetLocation[]? _notReplaceBlocks;
        private readonly AssetLocation? _teleportBlockCode;
        private readonly Dictionary<AssetLocation, int> _replaceBlockIds = new();

        private string _currentRock = "";
        private string _currentRoofing = "";
        private string _currentWood = "";
        private string _currentLantern = "";
        private string _currentChair = "";

        private readonly Random _rand = new();
        private readonly bool _ruin;

        public StructureBlockResolver(AssetLocation[] blockCodes, AssetLocation[]? notReplaceBlocks, AssetLocation? teleportBlockCode, bool ruin)
        {
            _ruin = ruin;
            _blockCodes = blockCodes;
            _notReplaceBlocks = notReplaceBlocks;
            _teleportBlockCode = teleportBlockCode;

            if (_teleportBlockCode != null && !_blockCodes.Contains(_teleportBlockCode))
            {
                _blockCodes = _blockCodes.Append(_teleportBlockCode);
            }
        }

        public void InitNew(IBlockAccessor blockAccessor, BlockPos pos, LCGRandom rand, TeleportSchematicStructure schematic, ILogger logger)
        {
            _replaceBlockIds.Clear();

            _currentRock = GetRock(blockAccessor, pos);
            _currentRoofing = Shingles[rand.NextInt(Shingles.Length)];
            _currentWood = Woods[rand.NextInt(Woods.Length)];
            _currentLantern = Lanterns[rand.NextInt(Lanterns.Length)];
            _currentChair = Chairs[rand.NextInt(Chairs.Length)];

            foreach (var blockCode in _blockCodes)
            {
                if (_notReplaceBlocks?.Contains(blockCode) ?? false)
                {
                    continue;
                }

                AssetLocation newCode = blockCode.Clone();

                if (Core.Config.DarknessMode)
                {
                    if (TryRemoveLantern(newCode))
                    {
                        continue;
                    }
                }

                if (!Core.Config.BiomlessTeleports)
                {
                    ReplaceGraniteRock(ref newCode);
                    ReplaceRoofing(ref newCode);
                    ReplaceWood(ref newCode);
                    ReplacePaintings(ref newCode, rand);
                    ReplaceVessels(ref newCode, rand);
                    ReplaceBrokenLanterns(ref newCode);
                    ReplaceChairs(ref newCode);
                    RandomizeCandles(ref newCode);
                }

                Block newBlock = blockAccessor.GetBlock(newCode);
                if (newBlock != null && !newBlock.IsMissing)
                {
                    _replaceBlockIds.Add(blockCode, newBlock.Id);
                }
                else
                {
                    logger.Warning($"Unknown new block {newCode}");
                }
            }

            if (_replaceBlockIds.Count > 0)
            {
                schematic.BlockEntityPlaced = OnBlockEntityPlaced;
            }
        }

        private void OnBlockEntityPlaced(int x, int y, int z, IBlockAccessor blockAccessor)
        {
            _tmpPos.Set(x, y, z);
            BlockEntity be = blockAccessor.GetBlockEntity(_tmpPos);

            if (be is BETeleport tbe)
            {
                if (_teleportBlockCode != null)
                {
                    if (_replaceBlockIds.TryGetValue(_teleportBlockCode, out int blockId))
                    {
                        Block block = blockAccessor.GetBlock(blockId);
                        tbe.FrameStack = new ItemStack(block);
                    }
                }
            }
            else if (be is BELantern lbe)
            {
                lbe.material = _currentLantern;
            }
            else if (_ruin && be is null)
            {
                Block block = blockAccessor.GetBlock(_tmpPos);

                if (!block.Code.Path.StartsWith("slantedroofing"))
                {
                    string[] plants =
                    {
                        "game:attachingplant-moss",
                        "game:attachingplant-lichen",
                        "game:attachingplant-barnacle"
                    };

                    for (int i = 1; i <= 32; i *= 2)
                    {
                        var face = BlockFacing.FromFlag(i);
                        if (block.SideSolid.OnSide(face) && _rand.Next(100) < 50)
                        {
                            string nextPlant = plants[_rand.Next(plants.Length)];
                            Block decorBlock = blockAccessor.GetBlock(new AssetLocation(nextPlant));
                            if (decorBlock != null)
                            {
                                blockAccessor.SetDecor(decorBlock, _tmpPos, face);
                            }
                        }
                    }
                }
            }
        }

        private void RandomizeCandles(ref AssetLocation code)
        {
            if (code.Path.StartsWith("buncheocandles"))
            {
                code.Path = $"buncheocandles-{_rand.Next(1, 10)}";
            }
        }

        private void ReplaceChairs(ref AssetLocation code)
        {
            if (code.Path.StartsWith("chair"))
            {
                code.Path = code.Path.Replace("plain", _currentChair);
            }
        }

        private void ReplaceBrokenLanterns(ref AssetLocation code)
        {
            if (code.Domain == Constants.ModId && code.Path.Contains("brokenlantern"))
            {
                code.Path = code.Path.Replace("copper", _currentLantern);
            }
        }

        private static void ReplaceVessels(ref AssetLocation code, LCGRandom rand)
        {
            if (code.Path.StartsWith("storagevessel"))
            {
                string newVessel = Vessels[rand.NextInt(Vessels.Length - 1)];
                code.Path = code.Path.Replace("burned", newVessel);
            }
        }

        private static void ReplacePaintings(ref AssetLocation code, LCGRandom rand)
        {
            if (code.Path.StartsWith("painting"))
            {
                string newPainting = Paintings[rand.NextInt(Paintings.Length - 1)];
                code.Path = code.Path.Replace("elk", newPainting);
                code.Path = code.Path.Replace("forestdawn", newPainting);
            }
        }

        private void ReplaceWood(ref AssetLocation code)
        {
            if (code.Path.Contains("agedebony"))
            {
                code.Path = code.Path.Replace("agedebony", _currentWood);
            }
            else
            {
                code.Path = code.Path.Replace("aged", _currentWood);
            }
        }

        private void ReplaceRoofing(ref AssetLocation code)
        {
            if (code.Path.StartsWith("clayshingle"))
            {
                code.Path = code.Path.Replace("blue", _currentRoofing);
            }

            if (code.Path.StartsWith("slantedroofing"))
            {
                code.Path = code.Path.Replace("blueclay", _currentRoofing + "clay");
            }
        }

        private void ReplaceGraniteRock(ref AssetLocation code)
        {
            // replace cobbleskull without textures
            if (code.Path.Contains("cobbleskull"))
            {
                if (_currentRock != "andesite" ||
                    _currentRock != "chalk" ||
                    _currentRock != "claystone" ||
                    _currentRock != "granite" ||
                    _currentRock != "shale" ||
                    _currentRock != "basalt")
                {
                    code = new AssetLocation("cobblestone-granite");
                }
            }

            if (code.Path.Contains("granite"))
            {
                code.Path = code.Path.Replace("granite", _currentRock);
            }
        }

        private bool TryRemoveLantern(AssetLocation code)
        {
            if (code.Path.StartsWith("paperlantern") ||
                code.Path.StartsWith("lantern"))
            {
                _replaceBlockIds.Add(code, 0);
                return true;
            }
            return false;
        }

        private static string GetRock(IBlockAccessor blockAccessor, BlockPos pos)
        {
            int topBlockY = blockAccessor.GetTerrainMapheightAt(pos);
            for (int i = topBlockY; i > 0; i--)
            {
                Block block = blockAccessor.GetBlock(pos.X, i, pos.Z);
                if (block.Code.Path.StartsWith("rock-"))
                {
                    string code = block.Code.Path.Replace("rock-", "");
                    Block rockBlock = blockAccessor.GetBlock(new AssetLocation("rock-" + code));
                    if (rockBlock != null)
                    {
                        return code;
                    }
                }
            }
            return "granite";
        }

        public Block? GetBlock(AssetLocation blockCode, IBlockAccessor blockAccessor)
        {
            if (_replaceBlockIds.TryGetValue(blockCode, out int blockId))
            {
                return blockAccessor.GetBlock(blockId);
            }
            else
            {
                return blockAccessor.GetBlock(blockCode);
            }

        }
    }
}
