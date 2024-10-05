using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace TeleportationNetwork
{
    // TODO: May be not thread-safe
    public class TeleportStructureBlockRandomizer
    {
        private readonly TeleportStructureGeneratorProperties _props;
        private readonly ICoreServerAPI _api;
        private readonly ILogger _logger;

        private string[] Woods { get; }
        private string[] Clays { get; }

        private Dictionary<AssetLocation, string[]> ReplaceByBlocks { get; } = [];
        private List<AssetLocation> LightBlocks { get; } = [];

        private TeleportStructureData _currentStructure = null!;

        private string _currentRock = "";
        private string _currentWood = "";
        private string _currentLantern = "";
        private string _currentClay = "";

        private readonly Dictionary<AssetLocation, int> _replaceBlockIds = [];

        public TeleportStructureBlockRandomizer(TeleportStructureGeneratorProperties props, ICoreServerAPI api, ILogger logger)
        {
            _props = props;
            _api = api;
            _logger = logger;

            var woodProps = api.Assets.Get("game:worldproperties/block/wood.json").ToObject<WoodWorldProperty>();
            Woods = woodProps.Variants.Select(v => v.Code.ToShortString()).AddItem("aged").ToArray();

            Clays = [
                "blue",
                "fire",
                "red",
                "brown"
            ];

            foreach (string lightBlock in props.LightBlocks)
            {
                Block[] blocks = api.World.SearchBlocks(new AssetLocation(lightBlock));
                LightBlocks.AddRange(blocks.Select(b => b.Code));
            }

            foreach (KeyValuePair<string, string> pair in props.ReplaceBlocks)
            {
                var foundCodes = Array.Empty<string>();

                var code = new AssetLocation(pair.Key);
                string pattern = pair.Value;

                Block[] blocks = api.World.SearchBlocks(code);
                foreach (Block block in blocks)
                {
                    string blockPattern = pattern;

                    if (pattern.Contains('*'))
                    {
                        string value = WildcardUtil.GetWildcardValue(code, block.Code);
                        blockPattern = pattern.Replace("*", value);
                    }

                    if (blockPattern.Contains("{any}"))
                    {
                        Block[] anyBlocks = api.World.SearchBlocks(new AssetLocation(blockPattern.Replace("{any}", "*")));
                        foundCodes = anyBlocks.Select(b => b.Code.ToString()).ToArray();
                    }
                    else
                    {
                        foundCodes = [blockPattern];
                    }

                    var cleanedCodes = new List<string>();
                    foreach (string fcode in foundCodes)
                    {
                        if (!props.ExcludeCodes.Contains(fcode))
                        {
                            cleanedCodes.Add(fcode);
                        }
                    }

                    ReplaceByBlocks.Add(block.Code, cleanedCodes.ToArray());
                }
            }
        }

        public void Next(IBlockAccessor blockAccessor, BlockPos pos, LCGRandom rand, TeleportStructureData structure)
        {
            string GetRock()
            {
                for (int i = blockAccessor.GetTerrainMapheightAt(pos); i > 0; i--)
                {
                    var bpos = new BlockPos(pos.X, i, pos.Y, pos.dimension);
                    Block block = blockAccessor.GetBlock(bpos);
                    if (block.Code.Path.StartsWith("rock-"))
                    {
                        string code = block.Code.Path.Replace("rock-", "");
                        Block rockBlock = blockAccessor.GetBlock(new AssetLocation($"rock-{code}"));
                        if (rockBlock != null)
                        {
                            return code;
                        }
                    }
                }
                return "granite";
            }

            _currentStructure = structure;

            _currentRock = GetRock();
            _currentWood = Woods[rand.NextInt(Woods.Length)];
            _currentClay = Clays[rand.NextInt(Clays.Length)];
            _currentLantern = _props.LanternMaterials[rand.NextInt(_props.LanternMaterials.Length)];

            GenerateBlockIdsForReplace(blockAccessor, rand);
        }

        private void GenerateBlockIdsForReplace(IBlockAccessor blockAccessor, LCGRandom rand)
        {
            _replaceBlockIds.Clear();

            foreach (var blockCode in _currentStructure.ContainsBlockCodes)
            {
                AssetLocation newCode = blockCode.Clone();

                if (!Core.Config.BiomlessTeleports && ReplaceByBlocks.TryGetValue(blockCode, out string[]? variants))
                {
                    string selected = variants[rand.NextInt(variants.Length)]
                        .Replace("{rock}", _currentRock)
                        .Replace("{wood}", _currentWood)
                        .Replace("{clay}", _currentClay);

                    // Cobble skull fix
                    if (_currentRock != "andesite" &&
                        _currentRock != "chalk" &&
                        _currentRock != "claystone" &&
                        _currentRock != "granite" &&
                        _currentRock != "shale" &&
                        _currentRock != "basalt")
                    {
                        selected = selected.Replace("cobbleskull", "cobblestone");
                    }

                    newCode = new AssetLocation(selected);
                }

                if (Core.Config.DarknessMode && LightBlocks.Contains(blockCode))
                {
                    newCode = new AssetLocation("air");
                }

                Block newBlock = blockAccessor.GetBlock(newCode);
                if (newBlock != null && !newBlock.IsMissing)
                {
                    _replaceBlockIds.Add(blockCode, newBlock.Id);
                }
                else
                {
                    _logger.Warning($"Unknown new block {newCode}");
                }
            }
        }

        public void AfterPlaceBlockRandomization(BlockPos pos, IBlockAccessor blockAccessor)
        {
            BlockEntity be = blockAccessor.GetBlockEntity(pos);

            if (be is BlockEntityTeleport tbe && tbe.FrameStack != null)
            {
                if (_replaceBlockIds.TryGetValue(tbe.FrameStack.Collectible.Code, out int blockId))
                {
                    Block block = blockAccessor.GetBlock(blockId);
                    tbe.FrameStack = new ItemStack(block);
                }
            }

            if (be is BELantern lbe)
            {
                lbe.material = _currentLantern;
            }

            if (_currentStructure.Props.Ruin && be is null)
            {
                Block block = blockAccessor.GetBlock(pos);

                bool attachDecor = true;
                foreach (string pattern in _props.IgnoreRuin)
                {
                    if (WildcardUtil.Match(new AssetLocation(pattern), block.Code))
                    {
                        attachDecor = false;
                        break;
                    }
                }

                if (attachDecor)
                {
                    Random rand = _api.World.Rand;
                    for (int i = 1; i <= 32; i *= 2)
                    {
                        var face = BlockFacing.FromFlag(i);
                        if (block.SideSolid.OnSide(face) && rand.Next(100) < 50)
                        {
                            string nextPlant = _props.RuinPlants[rand.Next(_props.RuinPlants.Length)];
                            Block decorBlock = blockAccessor.GetBlock(new AssetLocation(nextPlant));
                            if (decorBlock != null)
                            {
                                blockAccessor.SetDecor(decorBlock, pos, face);
                            }
                        }
                    }
                }
            }
        }

        public Block? GetRandomizedBlock(AssetLocation blockCode, IBlockAccessor blockAccessor)
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
