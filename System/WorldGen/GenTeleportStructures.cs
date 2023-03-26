using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace TeleportationNetwork
{
    public class GenTeleportStructures : ModSystem
    {
        private ICoreServerAPI _api = null!;

        private int _worldheight;
        private int _chunksize;
        private float _fullChance;

        private LCGRandom _strucRand = null!;
        private TeleportStructure[] _structures = null!;
        private IWorldGenBlockAccessor _worldgenBlockAccessor = null!;

        public override double ExecuteOrder() => 0.51; // vanilla structures is 0.5

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
            _chunksize = _api.WorldManager.ChunkSize;
            _worldheight = _api.WorldManager.MapSizeY;
            _strucRand = new LCGRandom(_api.World.Seed + 1091);

            IAsset asset = _api.Assets.Get("tpnet:worldgen/teleports.json");
            _structures = asset.ToObject<TeleportStructure[]>();
            for (int i = 0; i < _structures.Length; i++)
            {
                LCGRandom rand = new(_api.World.Seed + i + 512);
                _structures[i].Init(_api, rand);
                _fullChance += _structures[i].Chance;
            }
        }

        private void OnChunkColumnGenPostPass(IChunkColumnGenerateRequest request)
        {
            _worldgenBlockAccessor.BeginColumn();
            _strucRand.InitPositionSeed(request.ChunkX, request.ChunkZ);

            int posX = request.ChunkX * _chunksize;
            int posZ = request.ChunkZ * _chunksize;
            var pos = new BlockPos(posX, 0, posZ);

            if (!TeleportStructure.SatisfiesMinDistance(pos, _api.World))
            {
                return;
            }

            IMapChunk mapChunk = request.Chunks[0].MapChunk;
            IMapRegion region = mapChunk.MapRegion;
            ushort[] heightMap = mapChunk.WorldGenTerrainHeightMap;

            float chance = _strucRand.NextFloat() * _fullChance;
            for (int i = 0; i < _structures.Length; i++)
            {
                TeleportStructure struc = _structures[i];
                chance -= struc.Chance;
                if (chance <= 0)
                {
                    if (struc.Special && Core.Config.NoSpecialTeleports)
                    {
                        break;
                    }

                    for (int tries = 0; tries < Constants.TeleportTriesPerChunk; tries++)
                    {
                        int dx = _strucRand.NextInt(_chunksize);
                        int dz = _strucRand.NextInt(_chunksize);
                        int ySurface = heightMap[dz * _chunksize + dx];
                        if (ySurface <= 0 || ySurface >= _worldheight - 15) continue;

                        pos.Set(posX + dx, ySurface, posZ + dz);

                        if (struc.TryGenerate(_worldgenBlockAccessor, _api.World, pos))
                        {
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
                            break;
                        }
                    }
                    break;
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