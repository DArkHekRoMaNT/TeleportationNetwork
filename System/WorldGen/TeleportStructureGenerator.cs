using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace TeleportationNetwork
{
    public class TeleportStructureGeneratorSystem : ModSystem
    {
        private static int MaxHeightDiff => 5;

        private readonly List<TeleportStructureData> _structures = [];
        private List<BlockPos> _generatedStructures = [];

        private ICoreServerAPI _api = null!;
        private IWorldGenBlockAccessor _worldgenBlockAccessor = null!;
        private LCGRandom _chunkRandom = null!;
        private LCGRandom _posRandom = null!;
        private TeleportManager _teleportManager = null!;

        private int _chunksize;
        private int _worldheight;
        private float _fullChance;
        private TeleportStructureGeneratorProperties _props = null!;
        private TeleportStructureBlockRandomizer _blockRandomizer = null!;

        public override double ExecuteOrder() => 0.51; // vanilla structures is 0.5

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            _api = api;
            _teleportManager = api.ModLoader.GetModSystem<TeleportManager>();

            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.InitWorldGenerator(InitWorldGen, "standard");
                api.Event.ChunkColumnGeneration(OnChunkColumnGenPostPass, EnumWorldGenPass.TerrainFeatures, "standard");
                api.Event.GetWorldgenBlockAccessor((chunkProvider) =>
                {
                    _worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
                });
            }

            api.Event.GameWorldSave += OnSaveGame;
            api.Event.SaveGameLoaded += OnLoadGame;
        }

        private void OnSaveGame()
        {
            _api.WorldManager.SaveGame.StoreData("TPNetData_GeneratedStructures", _generatedStructures);
        }

        private void OnLoadGame()
        {
            _generatedStructures = _api.WorldManager.SaveGame.GetData<List<BlockPos>>("TPNetData_GeneratedStructures") ?? [];
        }

        private void InitWorldGen()
        {
            _chunkRandom = new LCGRandom(_api.World.Seed + 1091);
            _posRandom = new LCGRandom(_api.World.Seed + 5124);

            _chunksize = _api.WorldManager.ChunkSize;
            _worldheight = _api.WorldManager.MapSizeY;

            _props = _api.Assets.Get("tpnet:worldgen/teleports.json").ToObject<TeleportStructureGeneratorProperties>();
            _blockRandomizer = new TeleportStructureBlockRandomizer(_props, _api, Mod.Logger);

            foreach (TeleportStructureProperties structureProps in _props.Structures)
            {
                _structures.Add(new TeleportStructureData(structureProps, _api, Mod.Logger));
                _fullChance += structureProps.Chance;
            }
        }

        private void OnChunkColumnGenPostPass(IChunkColumnGenerateRequest request)
        {
            if (!TerraGenConfig.GenerateStructures)
            {
                return;
            }

            var pos = new BlockPos(request.ChunkX * _chunksize, 0, request.ChunkZ * _chunksize);
            if (!HasMinimalDistance(pos))
            {
                return;
            }

            _worldgenBlockAccessor.BeginColumn();
            _chunkRandom.InitPositionSeed(request.ChunkX, request.ChunkZ);

            IMapChunk mapChunk = request.Chunks[0].MapChunk;
            ushort[] heightMap = mapChunk.WorldGenTerrainHeightMap;

            float chance = _chunkRandom.NextFloat() * _fullChance;
            for (int i = 0; i < _structures.Count; i++)
            {
                TeleportStructureData structure = _structures[i];
                chance -= structure.Props.Chance;
                if (chance <= 0)
                {
                    if (structure.Props.Special && Core.Config.NoSpecialTeleports)
                    {
                        break;
                    }

                    for (int tries = 0; tries < Constants.TeleportTriesPerChunk; tries++)
                    {
                        int dx = _chunkRandom.NextInt(_chunksize);
                        int dz = _chunkRandom.NextInt(_chunksize);
                        int ySurface = heightMap[dz * _chunksize + dx];
                        if (ySurface <= 0 || ySurface >= _worldheight - 15)
                        {
                            continue;
                        }

                        if (TryGenerateStructure(structure, pos.X + dx, pos.Z + dz, pos.dimension))
                        {
                            break;
                        }
                    }

                    break;
                }
            }
        }

        private bool HasMinimalDistance(BlockPos pos)
        {
            lock (_generatedStructures)
            {
                foreach (BlockPos structurePos in _generatedStructures)
                {
                    int distance = structurePos.HorizontalManhattenDistance(pos);
                    if (distance > Core.Config.MinTeleportDistance)
                    {
                        continue;
                    }

                    return false;
                }
            }

            return true;
        }

        private bool TryGenerateStructure(TeleportStructureData structure, int x, int z, int dim)
        {
            var tmpPos = new BlockPos(dim);

            _posRandom.InitPositionSeed(x, z);
            structure.Randomize(_posRandom);

            // Check is area flat
            int max = 0;
            int min = _worldheight;
            var depths = new Dictionary<int, int>();
            for (int i = 0; i < structure.Teleport.SizeX; i++)
            {
                for (int j = 0; j < structure.Teleport.SizeZ; j++)
                {
                    int surfaceY = _worldgenBlockAccessor.GetTerrainMapheightAt(tmpPos.Set(x + i, 0, z + j));
                    max = Math.Max(max, surfaceY);
                    min = Math.Min(min, surfaceY);

                    if (depths.TryGetValue(surfaceY, out int _))
                    {
                        depths[surfaceY]++;
                    }
                    else
                    {
                        depths.Add(surfaceY, 1);
                    }
                }
            }

            float minimalAreaIsFlat = structure.Teleport.SizeX * structure.Teleport.SizeZ * 0.5f;
            if (depths[max] < minimalAreaIsFlat)
            {
                return false;
            }

            // Check max depth
            if (MaxHeightDiff < max - min)
            {
                return false;
            }

            // Check underwater
            int waterLevel = 0;
            for (int i = _worldgenBlockAccessor.GetTerrainMapheightAt(tmpPos.Set(x, 0, z)); i < _worldheight - 15; i++)
            {
                Block? block = _worldgenBlockAccessor.GetBlock(tmpPos.Set(x, i, z), BlockLayersAccess.Fluid);
                if (block != null && block.Id != 0)
                {
                    waterLevel++;
                }
            }

            if (structure.Props.Underwater && waterLevel == 0)
            {
                return false;
            }

            // Check glacier
            int glacierLevel = 0;
            for (int i = _worldgenBlockAccessor.GetTerrainMapheightAt(tmpPos.Set(x, 0, z)); i > 0; i--)
            {
                Block? block = _worldgenBlockAccessor.GetBlock(tmpPos.Set(x, i, z), BlockLayersAccess.MostSolid);
                if (block.Code.ToString() == "game:glacierice" || block.Code.ToString() == "game:showblock")
                {
                    glacierLevel++;
                }
                else
                {
                    break;
                }
            }
            min -= glacierLevel;

            // Set structure y pos
            int y = max + 1;
            if (!structure.Props.Underwater)
            {
                y += waterLevel;
            }
            y -= structure.Props.OffsetY;

            // Check min/max depth
            int depthWithWater = max + 1 + waterLevel - min - structure.Props.OffsetY;
            if (structure.Props.MaxDepth < depthWithWater || structure.Props.MinDepth > depthWithWater)
            {
                return false;
            }

            // Check area empty
            for (int i = 0; i < structure.Teleport.SizeX; i++)
            {
                for (int j = 0; j < structure.Teleport.SizeX; j++)
                {
                    for (int k = 0; k < structure.Teleport.SizeX; k++)
                    {
                        Block block = _worldgenBlockAccessor.GetBlock(tmpPos.Set(x, y, z), BlockLayersAccess.MostSolid);
                        if (block.Id != 0 && !structure.Props.Underwater)
                        {
                            return false;
                        }
                    }
                }
            }

            // Fix depth
            int extraDepth = 0;
            if (structure.Props.PillarAlwaysTop)
            {
                extraDepth = y - min - 1;

                if (extraDepth > 0 && structure.Base != null)
                {
                    extraDepth -= structure.Base.SizeY;
                }

                if (structure.Pillar != null)
                {
                    while (extraDepth > 0)
                    {
                        extraDepth -= structure.Pillar.SizeY;
                    }
                }

                extraDepth = extraDepth >= 0 ? 0 : -extraDepth;
            }

            // Generate
            lock (_generatedStructures)
            {
                var pos = new BlockPos(x, y + extraDepth, z, dim);
                var areas = new List<Cuboidi>();

                _blockRandomizer.Next(_worldgenBlockAccessor, pos, _posRandom, structure);

                _generatedStructures.Add(pos);

                areas.Add(PlaceSchematic(structure.Teleport, pos));

                int depth = pos.Y - min;
                if (depth > 0 && structure.Base != null)
                {
                    int height = structure.Base.SizeY;
                    pos.Y -= height;
                    areas.Add(PlaceSchematic(structure.Base, pos));
                    depth -= height;
                }

                if (structure.Pillar != null)
                {
                    while (depth > 0)
                    {
                        int height = structure.Pillar.SizeY;
                        pos.Y -= height;
                        areas.Add(PlaceSchematic(structure.Pillar, pos));
                        depth -= height;
                    }
                }

                AddBuildProtection(structure.Props, areas);
            }

            return true;
        }

        private Cuboidi PlaceSchematic(TeleportSchematicStructure schematic, BlockPos pos)
        {
            schematic.PlaceWithReplaceBlockIds(_worldgenBlockAccessor, _api.World, pos, _blockRandomizer);
            return new Cuboidi(pos.X, pos.Y, pos.Z, pos.X + schematic.SizeX, pos.Y + schematic.SizeY, pos.Z + schematic.SizeZ);
        }

        private void AddBuildProtection(TeleportStructureProperties props, List<Cuboidi> areas)
        {
            bool buildProtected = props.BuildProtected;
            if (Core.Config.TeleportBuildProtected == "on")
            {
                buildProtected = true;
            }
            else if (Core.Config.TeleportBuildProtected == "off")
            {
                buildProtected = false;
            }

            if (buildProtected)
            {
                _api.World.Claims.Add(new LandClaim
                {
                    Areas = areas,
                    Description = props.BuildProtectionDesc ?? "Teleport Perimeter",
                    ProtectionLevel = 10,
                    LastKnownOwnerName = props.BuildProtectionName ?? "Teleport",
                    AllowUseEveryone = true
                });
            }
        }
    }
}
