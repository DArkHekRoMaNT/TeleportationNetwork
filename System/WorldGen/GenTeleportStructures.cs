using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace TeleportationNetwork
{
    public class GenTeleportStructures : ModSystem
    {
        private int _chunksize;
        private float _fullChance;

        private ICoreServerAPI _api = null!;
        private LCGRandom _posRand = null!;
        private LCGRandom _strucRand = null!;
        private TeleportStructure[] _structures = null!;
        private IWorldGenBlockAccessor _worldgenBlockAccessor = null!;

        public override double ExecuteOrder() => 0.41; // vanilla structures is 0.5

        public override void StartServerSide(ICoreServerAPI api)
        {
            _api = api;
            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.InitWorldGenerator(InitWorldGen, "standard");
                api.Event.ChunkColumnGeneration(OnChunkColumnGenPostPass, EnumWorldGenPass.TerrainFeatures, "standard");
                api.Event.GetWorldgenBlockAccessor((chunkProvider) =>
                {
                    _worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
                });
            }
        }

        private void InitWorldGen()
        {
            _strucRand = new LCGRandom(_api.World.Seed + 1091);
            _posRand = new LCGRandom(_api.World.Seed + 19861);

            _chunksize = _api.WorldManager.ChunkSize;

            IAsset asset = _api.Assets.Get("tpnet:worldgen/teleports.json");
            _structures = asset.ToObject<TeleportStructure[]>();
            for (int i = 0; i < _structures.Length; i++)
            {
                LCGRandom rand = new(_api.World.Seed + i + 512);
                _structures[i].Init(_api, rand, Mod.Logger);
                _fullChance += _structures[i].Chance;
            }
        }

        public BlockPos GetTeleportPosHere(int chunkX, int chunkZ)
        {
            int centerX = _api.WorldManager.MapSizeX / 2;
            int centerZ = _api.WorldManager.MapSizeZ / 2;

            int gridSize = Core.Config.TeleportGridSize;
            int centerOffsetX = centerX % gridSize;
            int centerOffsetZ = centerZ % gridSize;

            int teleportX = chunkX * _chunksize / gridSize;
            int teleportZ = chunkZ * _chunksize / gridSize;
            _posRand.InitPositionSeed(teleportX, teleportZ);

            int gridAreaRadius = Core.Config.TeleportGridAreaRadius;
            int offsetX = _posRand.NextInt(gridAreaRadius * 2) - gridAreaRadius;
            int offsetZ = _posRand.NextInt(gridAreaRadius * 2) - gridAreaRadius;

            int posX = teleportX * gridSize + centerOffsetX + offsetX;
            int posZ = teleportZ * gridSize + centerOffsetZ + offsetZ;

            return new BlockPos(posX, 0, posZ);
        }

        private void OnChunkColumnGenPostPass(IChunkColumnGenerateRequest request)
        {
            if (!TerraGenConfig.GenerateStructures)
            {
                return;
            }

            var pos = GetTeleportPosHere(request.ChunkX, request.ChunkZ);

            int chunkPosX = request.ChunkX * _chunksize;
            int chunkPosZ = request.ChunkZ * _chunksize;

            if (chunkPosX > pos.X || pos.X >= chunkPosX + _chunksize ||
                chunkPosZ > pos.Z || pos.Z >= chunkPosZ + _chunksize ||
                _structures.Length == 0)
            {
                return;
            }

            _worldgenBlockAccessor.BeginColumn();

            IMapRegion region = request.Chunks[0].MapChunk.MapRegion;

            TeleportStructure? towerStruc = _structures.FirstOrDefault(e => e.IsTower);

            if (Core.Config.NoSpecialTeleports || towerStruc == null ||
                MaxHeightDiff(21, _worldgenBlockAccessor, pos) < 10)
            {
                float chance = _strucRand.NextFloat() * _fullChance;
                for (int i = 0, k = 0; k < _structures.Length * 2; i++, k++)
                {
                    if (i >= _structures.Length)
                    {
                        i = 0;
                    }

                    TeleportStructure struc = _structures[i];
                    chance -= struc.Chance;
                    if (chance <= 0)
                    {
                        if (struc.Special && Core.Config.NoSpecialTeleports)
                        {
                            continue;
                        }

                        GenerateStructure(struc);
                        break;
                    }
                }
            }
            else
            {
                GenerateStructure(towerStruc);
            }

            static int MaxHeightDiff(int size, IWorldGenBlockAccessor blockAccessor, BlockPos pos)
            {
                var tmp = new BlockPos();
                int min = int.MaxValue;
                int max = 0;

                for (int i = 0; i < size; i++)
                {
                    for (int j = 0; j < size; j++)
                    {
                        tmp.Set(pos.X + i, 0, pos.Z + j);
                        int y = blockAccessor.GetTerrainMapheightAt(tmp);
                        min = Math.Min(y, min);
                        max = Math.Max(y, max);
                    }
                }

                return max - min;
            }

            void GenerateStructure(TeleportStructure struc)
            {
                lock (region.GeneratedStructures)
                {
                    struc.Generate(_worldgenBlockAccessor, _api.World, pos);
                    Cuboidi loc = struc.LastPlacedSchematicLocation;

                    string code = struc.Code;
                    if (struc.LastPlacedSchematic != null)
                    {
                        code += "/" + struc.LastPlacedSchematic.FromFileName;
                    }

                    region.GeneratedStructures.Add(new GeneratedStructure()
                    {
                        Code = code,
                        Group = Constants.TeleportStructureGroup,
                        Location = loc.Clone()
                    });
                    region.DirtyForSaving = true;

                    AddBuildProtection(struc, loc);
                }
            }
        }

        private void AddBuildProtection(TeleportStructure struc, Cuboidi loc)
        {
            bool buildProtected = struc.BuildProtected;
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
                _api.World.Claims.Add(new LandClaim()
                {
                    Areas = new List<Cuboidi>() { loc.Clone() },
                    Description = struc.BuildProtectionDesc ?? "Teleport Perimeter",
                    ProtectionLevel = 10,
                    LastKnownOwnerName = struc.BuildProtectionName ?? "Teleport",
                    AllowUseEveryone = true
                });
            }
        }
    }
}
