using CommonLib.Utils;
using Newtonsoft.Json;
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
        public static int Size => 11;

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

        private readonly BlockPos _tmpPos = new();

        private int _seaLevel;
        private LCGRandom _rand = null!;
        private ILogger _logger = null!;

        private TeleportSchematicStructure[][] _schematicDatas = null!;
        private TeleportSchematicStructure[] _pillarDatas = null!;
        private TeleportSchematicStructure[] _pillarBaseDatas = null!;
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

        public bool TryGenerate(IBlockAccessor blockAccessor, IWorldAccessor world, BlockPos pos)
        {
            if (!CheckArea(pos, blockAccessor, world, out bool generatePillar, out int lowerY))
            {
                return false;
            }

            _rand.InitPositionSeed(pos.X, pos.Z);

            int number = _rand.NextInt(_schematicDatas.Length);
            int orientation = _rand.NextInt(4);
            var schematic = _schematicDatas[number][orientation];

            _resolver.InitNew(blockAccessor, pos, _rand, schematic, _logger);

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

            schematic.PlaceWithReplaceBlockIds(blockAccessor, world, pos, _resolver);

            LastPlacedSchematic = schematic;
            LastPlacedSchematicLocation.Set(pos.X, pos.Y, pos.Z,
                pos.X + schematic.SizeX, pos.Y + schematic.SizeY, pos.Z + schematic.SizeZ);

            return true;

            bool CheckArea(BlockPos pos, IBlockAccessor blockAccessor,
            IWorldAccessor worldForCollectibleResolve, out bool generatePillar, out int lowerY)
            {
                lowerY = pos.Y;
                generatePillar = false;

                if (!SatisfiesMinDistance(pos, worldForCollectibleResolve) ||
                    IsStructureAt(pos, worldForCollectibleResolve))
                {
                    return false;
                }

                if (IsLiquid(pos.X, pos.Y + 1, pos.Z, blockAccessor))
                {
                    pos.Y = _seaLevel - 1;
                    if (IsLiquid(pos.X, pos.Y + 1, pos.Z, blockAccessor))
                    {
                        return false;
                    }
                    generatePillar = true;
                }

                pos.Y = pos.Y + 1 + OffsetY;

                int areaSize = Size * Size;
                bool isFlatArea = IsFlatArea(pos, blockAccessor, out int lowerCount, out lowerY);
                if (!isFlatArea ||
                    !generatePillar && lowerCount > areaSize / 4 ||
                    pos.Y - lowerY > Constants.MaxPillarHeight)
                {
                    return false;
                }

                if (!IsFreeArea(pos, blockAccessor))
                {
                    return false;
                }

                generatePillar = generatePillar || lowerCount != 0;
                return true;
            }

            bool IsLiquid(int x, int y, int z, IBlockAccessor blockAccessor)
            {
                _tmpPos.Set(x, y, z);
                return blockAccessor.GetBlock(_tmpPos, BlockLayersAccess.Fluid).IsLiquid();
            }

            bool IsFlatArea(BlockPos pos, IBlockAccessor blockAccessor, out int lowerCount, out int lowerY)
            {
                lowerCount = 0;
                lowerY = pos.Y - 1;

                // check flat space
                for (int i = 0; i < Size; i++)
                {
                    for (int j = 0; j < Size; j++)
                    {
                        _tmpPos.Set(pos.X + i, 0, pos.Z + j);
                        int y = blockAccessor.GetTerrainMapheightAt(_tmpPos);
                        if (y < pos.Y - 1)
                        {
                            lowerCount++;
                            if (y < lowerY)
                            {
                                lowerY = y;
                            }
                        }
                        else if (y > pos.Y - 1)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            bool IsFreeArea(BlockPos pos, IBlockAccessor blockAccessor)
            {
                for (int i = 0; i < Size; i++)
                {
                    for (int j = 0; j < Size; j++)
                    {
                        for (int k = 0; k < Size; k++)
                        {
                            Block block = blockAccessor.GetBlock(pos.X + i, pos.Y + j, pos.Z + k);
                            if (block.Replaceable < 6000)
                            {
                                return false;
                            }
                        }
                    }
                }
                return true;
            }

            static bool IsStructureAt(BlockPos pos, IWorldAccessor world)
            {
                int rx = pos.X / world.BlockAccessor.RegionSize;
                int rz = pos.Z / world.BlockAccessor.RegionSize;

                IMapRegion mapregion = world.BlockAccessor.GetMapRegion(rx, rz);
                if (mapregion == null) return false;

                foreach (var val in mapregion.GeneratedStructures)
                {
                    if (val.Location.Contains(pos) || val.Location.Contains(pos.X, pos.Y - 3, pos.Z))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static bool SatisfiesMinDistance(BlockPos pos, IWorldAccessor world)
        {
            int minDistance = Core.Config.MinTeleportDistance;
            int regSize = world.BlockAccessor.RegionSize;

            int mapRegionSizeX = world.BlockAccessor.MapSizeX / regSize;
            int mapRegionSizeZ = world.BlockAccessor.MapSizeZ / regSize;

            int x1 = pos.X - minDistance;
            int z1 = pos.Z - minDistance;
            int x2 = pos.X + minDistance;
            int z2 = pos.Z + minDistance;

            int minDistSq = minDistance * minDistance;

            int minrx = GameMath.Clamp((x1 / regSize) - 1, 0, mapRegionSizeX);
            int minrz = GameMath.Clamp((z1 / regSize) - 1, 0, mapRegionSizeZ);
            int maxrx = GameMath.Clamp((x2 / regSize) + 1, 0, mapRegionSizeX);
            int maxrz = GameMath.Clamp((z2 / regSize) + 1, 0, mapRegionSizeZ);

            for (int rx = minrx; rx <= maxrx; rx++)
            {
                for (int rz = minrz; rz <= maxrz; rz++)
                {
                    IMapRegion mapregion = world.BlockAccessor.GetMapRegion(rx, rz);
                    if (mapregion == null) continue;

                    foreach (GeneratedStructure structure in mapregion.GeneratedStructures)
                    {
                        if (structure.Group == Constants.TeleportStructureGroup &&
                            structure.Location.Center.SquareDistanceTo(pos.X, pos.Y, pos.Z) < minDistSq)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}
