using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace TeleportationNetwork
{
    public class TeleportStructure
    {
        [JsonProperty, JsonRequired] public string Code { get; private set; } = null!;
        [JsonProperty, JsonRequired] public string[] Schematics { get; private set; } = null!;
        [JsonProperty] public AssetLocation[]? ReplaceWithBlocklayers { get; private set; }
        [JsonProperty] public bool BuildProtected { get; private set; }
        [JsonProperty] public string? BuildProtectionDesc { get; private set; }
        [JsonProperty] public string? BuildProtectionName { get; private set; }
        [JsonProperty] public float Chance { get; private set; } = 0.05f;
        [JsonProperty] public int OffsetY { get; private set; } = 0;

        public BlockSchematicStructure[][] SchematicDatas { get; private set; } = null!;
        public BlockSchematicStructure[] PillarDatas { get; private set; } = null!;
        public BlockSchematicStructure[] PillarBaseDatas { get; private set; } = null!;
        public int[] ReplaceBlockIds { get; private set; } = null!;
        public Cuboidi LastPlacedSchematicLocation { get; } = new();
        public BlockSchematicStructure? LastPlacedSchematic { get; private set; }

        private int _seaLevel;
        private LCGRandom _rand = null!;
        private readonly BlockPos _tmpPos = new();

        public void Init(ICoreServerAPI api, LCGRandom rand)
        {
            _rand = rand;
            _seaLevel = api.World.SeaLevel;

            InitSchematicData(api);

            if (ReplaceWithBlocklayers != null)
            {
                ReplaceBlockIds = new int[ReplaceWithBlocklayers.Length];
                for (int i = 0; i < ReplaceBlockIds.Length; i++)
                {
                    Block block = api.World.GetBlock(ReplaceWithBlocklayers[i]);
                    if (block == null)
                    {
                        throw new Exception(string.Format("Schematic with code {0} has replace" +
                            " block layer {1} defined, but no such block found!",
                            Code, ReplaceWithBlocklayers[i]));
                    }
                    else
                    {
                        ReplaceBlockIds[i] = block.Id;
                    }
                }
            }
        }

        private void InitSchematicData(ICoreServerAPI api)
        {
            IAsset asset = api.Assets.Get("game:worldgen/rockstrata.json");
            var rockstrata = asset.ToObject<RockStrataConfig>();

            asset = api.Assets.Get("game:worldgen/blocklayers.json");
            var blockLayerConfig = asset.ToObject<BlockLayerConfig>();
            blockLayerConfig.ResolveBlockIds(api, rockstrata);

            var schematics = new List<BlockSchematicStructure[]>();
            for (int i = 0; i < Schematics.Length; i++)
            {
                var schematic = LoadSchematic(api, blockLayerConfig, Schematics[i]);
                if (schematic != null)
                {
                    schematics.Add(schematic);
                }
            }
            SchematicDatas = schematics.ToArray();

            PillarDatas = LoadSchematic(api, blockLayerConfig, "tpnet/pillar")!;
            PillarBaseDatas = LoadSchematic(api, blockLayerConfig, "tpnet/pillar-base")!;
        }

        private static BlockSchematicStructure[]? LoadSchematic(ICoreServerAPI api, BlockLayerConfig config, string name)
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
                var schematic = asset.ToObject<BlockSchematicStructure>();
                if (schematic == null)
                {
                    api.World.Logger.Warning("Could not load {0}", name);
                    continue;
                }

                schematic.FromFileName = asset.Name;

                var rotatedSchematics = new BlockSchematicStructure[4];
                rotatedSchematics[0] = schematic;

                for (int k = 0; k < 4; k++)
                {
                    if (k > 0)
                    {
                        rotatedSchematics[k] = rotatedSchematics[0].Clone();
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

        public bool TryGenerate(IBlockAccessor blockAccessor,
            IWorldAccessor worldForCollectibleResolve, BlockPos pos,
            int climateUpLeft, int climateUpRight, int climateBotLeft, int climateBotRight)
        {
            pos.Y += OffsetY;
            _rand.InitPositionSeed(pos.X, pos.Z);

            int num = _rand.NextInt(SchematicDatas.Length);
            int orient = _rand.NextInt(4);
            var schematic = SchematicDatas[num][orient];

            int widthHalf = (int)Math.Ceiling(schematic.SizeX / 2f);
            int lenghtHalf = (int)Math.Ceiling(schematic.SizeZ / 2f);

            int width = schematic.SizeX;
            int length = schematic.SizeZ;
            int height = schematic.SizeY;

            _tmpPos.Set(pos.X + widthHalf, 0, pos.Z + lenghtHalf);
            int centerY = blockAccessor.GetTerrainMapheightAt(_tmpPos);
            if (!OnFlatGround(pos, width, length, centerY, blockAccessor))
            {
                return false;
            }

            pos.Y = centerY + 1 + OffsetY;

            bool generatePillar = false;
            if (SubMergedInWater(pos, width, length, height, blockAccessor))
            {
                generatePillar = true;
                pos.Y = _seaLevel + 1;
                if (SubMergedInWater(pos, width, length, height, blockAccessor))
                {
                    return false;
                }
            }

            if (!SatisfiesMinDistance(pos, worldForCollectibleResolve)) return false;
            if (IsStructureAt(pos, worldForCollectibleResolve)) return false;

            if (generatePillar)
            {
                var pillarSchematic = SchematicDatas[num][orient];
                for (int i = centerY + 1; i < pos.Y; i++)
                {
                    pos.Y = i;
                    pillarSchematic.PlaceRespectingBlockLayers(blockAccessor, worldForCollectibleResolve, pos,
                        climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, ReplaceBlockIds);
                }
                pos.Y = _seaLevel + 1 + OffsetY;
            }

            schematic.PlaceRespectingBlockLayers(blockAccessor, worldForCollectibleResolve, pos,
                climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, ReplaceBlockIds);

            LastPlacedSchematic = schematic;
            LastPlacedSchematicLocation.Set(pos.X, pos.Y, pos.Z,
            pos.X + schematic.SizeX, pos.Y + schematic.SizeY, pos.Z + schematic.SizeZ);
            return true;
        }

        private static bool SubMergedInWater(BlockPos pos, int width, int length, int height, IBlockAccessor blockAccessor)
        {
            int widthHalf = (int)Math.Ceiling(width / 2f);
            int lenghtHalf = (int)Math.Ceiling(length / 2f);

            int[] dx = new int[] { widthHalf, width, width, 0, 0 };
            int[] dz = new int[] { lenghtHalf, length, 0, length, 0 };

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < dx.Length; j++)
                {
                    Block block = blockAccessor.GetBlock(pos.X + dx[j], pos.Y + i, pos.Z + dz[j], BlockLayersAccess.Fluid);
                    if (block.IsLiquid())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool OnFlatGround(BlockPos pos, int width, int length, int centerY, IBlockAccessor blockAccessor)
        {
            // Probe all 4 corners + center if they are on the same height

            _tmpPos.Set(pos.X, 0, pos.Z);
            int topLeftY = blockAccessor.GetTerrainMapheightAt(_tmpPos);

            _tmpPos.Set(pos.X + width, 0, pos.Z);
            int topRightY = blockAccessor.GetTerrainMapheightAt(_tmpPos);

            _tmpPos.Set(pos.X, 0, pos.Z + length);
            int botLeftY = blockAccessor.GetTerrainMapheightAt(_tmpPos);

            _tmpPos.Set(pos.X + width, 0, pos.Z + length);
            int botRightY = blockAccessor.GetTerrainMapheightAt(_tmpPos);

            int diff = GameMath.Max(centerY, topLeftY, topRightY, botLeftY, botRightY) -
                GameMath.Min(centerY, topLeftY, topRightY, botLeftY, botRightY);

            return diff == 0;
        }

        public static bool IsStructureAt(BlockPos pos, IWorldAccessor world)
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

        public static bool SatisfiesMinDistance(BlockPos pos, IWorldAccessor world)
        {
            int minDistance = Core.Config.MinTeleportDistance;
            if (minDistance < 1) return true;

            int regSize = world.BlockAccessor.RegionSize;

            int mapRegionSizeX = world.BlockAccessor.MapSizeX / regSize;
            int mapRegionSizeZ = world.BlockAccessor.MapSizeZ / regSize;

            int x1 = pos.X - minDistance;
            int z1 = pos.Z - minDistance;
            int x2 = pos.X + minDistance;
            int z2 = pos.Z + minDistance;

            // Definition: Max structure size is 256x256x256
            //int maxStructureSize = 256;

            int minDistSq = minDistance * minDistance;

            int minrx = GameMath.Clamp(x1 / regSize, 0, mapRegionSizeX);
            int minrz = GameMath.Clamp(z1 / regSize, 0, mapRegionSizeZ);
            int maxrx = GameMath.Clamp(x2 / regSize, 0, mapRegionSizeX);
            int maxrz = GameMath.Clamp(z2 / regSize, 0, mapRegionSizeZ);

            for (int rx = minrx; rx <= maxrx; rx++)
            {
                for (int rz = minrz; rz <= maxrz; rz++)
                {
                    IMapRegion mapregion = world.BlockAccessor.GetMapRegion(rx, rz);
                    if (mapregion == null) continue;

                    foreach (var val in mapregion.GeneratedStructures)
                    {
                        if (val.Group == Constants.TeleportStructureGroup && val.Location.Center.SquareDistanceTo(pos.X, pos.Y, pos.Z) < minDistSq)
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
