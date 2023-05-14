using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace TeleportationNetwork
{
    public class TeleportStructure
    {
        public static int PillarSize => 11;

        [JsonProperty, JsonRequired] public string Code { get; private set; } = null!;
        [JsonProperty, JsonRequired] public string[] Schematics { get; private set; } = null!;
        [JsonProperty] public AssetLocation[]? NotReplaceBlocks { get; private set; }
        [JsonProperty] public AssetLocation? TeleportBlockCode { get; private set; }
        [JsonProperty] public bool Ruin { get; private set; }
        [JsonProperty] public bool Special { get; private set; }
        [JsonProperty] public bool BuildProtected { get; private set; }
        [JsonProperty] public string? BuildProtectionDesc { get; private set; }
        [JsonProperty] public string? BuildProtectionName { get; private set; }
        [JsonProperty] public float Chance { get; private set; } = 0.05f;
        [JsonProperty] public int OffsetY { get; private set; } = 0;

        public Cuboidi LastPlacedSchematicLocation { get; } = new();
        public BlockSchematicStructure? LastPlacedSchematic { get; private set; }

        public bool IsTower => Code.Contains("tower");

        private readonly BlockPos _tmpPos = new();

        private int _seaLevel;
        private LCGRandom _rand = null!;
        private ILogger _logger = null!;

        private TeleportSchematicStructure[][] _schematicDatas = null!;
        private TeleportSchematicStructure[] _pillarDatas = null!;
        private TeleportSchematicStructure[] _pillarBaseDatas = null!;
        private TeleportSchematicStructure[] _towerStairsDatas = null!;
        private StructureBlockResolver _resolver = null!;

        public void Init(ICoreServerAPI api, LCGRandom rand, ILogger logger)
        {
            _rand = rand;
            _seaLevel = api.World.SeaLevel;
            _logger = logger;

            InitSchematicData(api);
            InitResolver();

            void InitSchematicData(ICoreServerAPI api)
            {
                IAsset asset = api.Assets.Get("game:worldgen/rockstrata.json");
                var rockstrata = asset.ToObject<RockStrataConfig>();

                asset = api.Assets.Get("game:worldgen/blocklayers.json");
                var blockLayerConfig = asset.ToObject<BlockLayerConfig>();
                blockLayerConfig.ResolveBlockIds(api, rockstrata);

                var schematics = new List<TeleportSchematicStructure[]>();
                for (int i = 0; i < Schematics.Length; i++)
                {
                    var schematic = LoadSchematic(api, blockLayerConfig, Schematics[i]);
                    if (schematic != null)
                    {
                        schematics.Add(schematic);
                    }
                }
                _schematicDatas = schematics.ToArray();

                _pillarDatas = LoadSchematic(api, blockLayerConfig, "tpnet/teleport/pillar")!;
                _pillarBaseDatas = LoadSchematic(api, blockLayerConfig, "tpnet/teleport/pillar-base")!;
                _towerStairsDatas = LoadSchematic(api, blockLayerConfig, "tpnet/teleport/tower-stairs")!;
            }

            void InitResolver()
            {
                var blockCodes = new List<AssetLocation>();

                foreach (var schematic in _schematicDatas)
                {
                    foreach (var rotatedSchematic in schematic)
                    {
                        blockCodes.AddRange(rotatedSchematic.BlockCodes.Values.ToArray());
                    }
                }

                for (int i = 0; i < 4; i++)
                {
                    blockCodes.AddRange(_pillarBaseDatas[i].BlockCodes.Values.ToArray());
                    blockCodes.AddRange(_pillarDatas[i].BlockCodes.Values.ToArray());
                    blockCodes.AddRange(_towerStairsDatas[i].BlockCodes.Values.ToArray());
                }

                _resolver = new(blockCodes.Distinct().ToArray(), NotReplaceBlocks, TeleportBlockCode, Ruin);
            }

            TeleportSchematicStructure[]? LoadSchematic(ICoreServerAPI api, BlockLayerConfig config, string name)
            {
                IAsset[] assets;

                if (name.EndsWith("*"))
                {
                    string subName = name.Substring(0, name.Length - 1);
                    assets = api.Assets.GetManyInCategory("worldgen", "schematics/" + subName).ToArray();
                }
                else
                {
                    assets = new IAsset[] { api.Assets.Get("worldgen/schematics/" + name + ".json") };
                }

                foreach (IAsset asset in assets)
                {
                    var schematic = asset.ToObject<TeleportSchematicStructure>();
                    if (schematic == null)
                    {
                        _logger.Warning("Could not load {0}", name);
                        continue;
                    }

                    schematic.FromFileName = asset.Name;

                    var rotatedSchematics = new TeleportSchematicStructure[4];
                    rotatedSchematics[0] = schematic;

                    for (int k = 0; k < 4; k++)
                    {
                        if (k > 0)
                        {
                            rotatedSchematics[k] = rotatedSchematics[0].Copy();
                            rotatedSchematics[k].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, k * 90);
                        }
                        rotatedSchematics[k].blockLayerConfig = config;
                        rotatedSchematics[k].Init(api.World.BlockAccessor);
                        rotatedSchematics[k].LoadMetaInformationAndValidate(api.World.BlockAccessor,
                            api.World, schematic.FromFileName);
                    }

                    return rotatedSchematics;
                }

                return null;
            }
        }

        public void Generate(IBlockAccessor blockAccessor, IWorldAccessor world, BlockPos pos)
        {
            _rand.InitPositionSeed(pos.X, pos.Z);

            int number = _rand.NextInt(_schematicDatas.Length);
            int orientation = _rand.NextInt(4);
            TeleportSchematicStructure schematic = _schematicDatas[number][orientation];

            bool generatePillar = false;

            pos.Y = blockAccessor.GetTerrainMapheightAt(pos);

            if (IsLiquid(pos.X, pos.Y + 1, pos.Z, blockAccessor))
            {
                pos.Y = _seaLevel;
                for (int i = 0; pos.Y < blockAccessor.MapSizeY; i++)
                {
                    if (!IsLiquid(pos.X, _seaLevel + i, pos.Z, blockAccessor))
                    {
                        pos.Y = _seaLevel + i;
                        break;
                    }
                }
                generatePillar = true;
            }

            int topY = 0;
            int lowerY = pos.Y - 1;

            for (int i = 0; i < PillarSize; i++)
            {
                for (int j = 0; j < PillarSize; j++)
                {
                    _tmpPos.Set(pos.X + i, 0, pos.Z + j);
                    int y = blockAccessor.GetTerrainMapheightAt(_tmpPos);
                    topY = Math.Max(y, topY);
                    lowerY = Math.Min(y, lowerY);
                }
            }

            int heightDiff = topY - lowerY;
            if (heightDiff > 0 && heightDiff <= (IsTower ? 0 : 2))
            {
                topY -= heightDiff;
                heightDiff = 0;
            }

            generatePillar = generatePillar || heightDiff > 0;
            pos.Y = topY + 1 + OffsetY;

            _resolver.InitNew(blockAccessor, pos, _rand, schematic, _logger);


            if (IsTower)
            {
                if (generatePillar)
                {
                    var towerStairsSchematic = _towerStairsDatas[orientation];
                    int shift = towerStairsSchematic.SizeY - heightDiff % towerStairsSchematic.SizeY;
                    lowerY -= shift - 1;

                    for (int i = lowerY; i < pos.Y; i += towerStairsSchematic.SizeY)
                    {
                        _tmpPos.Set(pos.X, i, pos.Z);
                        towerStairsSchematic.PlaceWithReplaceBlockIds(blockAccessor, world, _tmpPos, _resolver);
                    }
                }
            }
            else
            {
                if (generatePillar)
                {
                    var pillarBaseSchematic = _pillarBaseDatas[orientation];
                    _tmpPos.Set(pos.X, pos.Y - 2, pos.Z);
                    pillarBaseSchematic.PlaceWithReplaceBlockIds(blockAccessor, world, _tmpPos, _resolver);

                    var pillarSchematic = _pillarDatas[orientation];
                    for (int i = lowerY; i < pos.Y - 2; i++)
                    {
                        _tmpPos.Set(pos.X, i, pos.Z);
                        pillarSchematic.PlaceWithReplaceBlockIds(blockAccessor, world, _tmpPos, _resolver);
                    }
                }
            }

            schematic.PlaceWithReplaceBlockIds(blockAccessor, world, pos, _resolver);

            LastPlacedSchematic = schematic;
            LastPlacedSchematicLocation.Set(pos.X, pos.Y, pos.Z,
                pos.X + schematic.SizeX, pos.Y + schematic.SizeY, pos.Z + schematic.SizeZ);

            bool IsLiquid(int x, int y, int z, IBlockAccessor blockAccessor)
            {
                _tmpPos.Set(x, y, z);
                return blockAccessor.GetBlock(_tmpPos, BlockLayersAccess.Fluid).IsLiquid();
            }
        }
    }
}
