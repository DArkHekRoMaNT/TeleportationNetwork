using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace TeleportationNetwork
{
    public class GenTeleportStructures : ModSystem
    {
        private ICoreServerAPI _api = null!;

        private int _worldheight;
        private int _chunksize;
        private int _regionChunkSize;

        private LCGRandom _strucRand = null!; // Deterministic random
        private TeleportStructure[] _structures = null!;
        private TeleportStructure[] _shuffledStructures = null!;
        private IWorldGenBlockAccessor _worldgenBlockAccessor = null!;

        public override double ExecuteOrder() => 0.5;

        public override void StartServerSide(ICoreServerAPI api)
        {
            _api = api;

            if (ModStdWorldGen.DoDecorationPass)
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
            _regionChunkSize = _api.WorldManager.RegionSize / _chunksize;

            _strucRand = new LCGRandom(_api.WorldManager.Seed + 1091);

            IAsset asset = _api.Assets.Get("tpnet:worldgen/teleports.json");
            _structures = asset.ToObject<TeleportStructure[]>();
            for (int i = 0; i < _structures.Length; i++)
            {
                LCGRandom rand = new(_api.World.Seed + i + 512);
                _structures[i].Init(_api, rand);
            }
            _shuffledStructures = new TeleportStructure[_structures.Length];
        }

        private void OnChunkColumnGenPostPass(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            _worldgenBlockAccessor.BeginColumn();

            var pos = new BlockPos();

            _strucRand.InitPositionSeed(chunkX, chunkZ);

            // We need to make a copy each time to preserve determinism
            // which is crucial for the translocator to find an exit point
            for (int i = 0; i < _shuffledStructures.Length; i++)
            {
                _shuffledStructures[i] = _structures[i];
            }

            _shuffledStructures.Shuffle(_strucRand);

            IMapRegion region = chunks[0].MapChunk.MapRegion;
            var climateMap = region.ClimateMap;
            int rlX = chunkX % _regionChunkSize;
            int rlZ = chunkZ % _regionChunkSize;

            float facC = (float)climateMap.InnerSize / _regionChunkSize;
            var climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC));
            var climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC));
            var climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC + facC));
            var climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC + facC));

            ushort[] heightMap = chunks[0].MapChunk.WorldGenTerrainHeightMap;

            for (int i = 0; i < _shuffledStructures.Length; i++)
            {
                TeleportStructure struc = _shuffledStructures[i];

                float chance = struc.Chance;

                while (chance-- > _strucRand.NextFloat())
                {
                    int dx = _strucRand.NextInt(_chunksize);
                    int dz = _strucRand.NextInt(_chunksize);
                    int ySurface = heightMap[dz * _chunksize + dx];
                    if (ySurface <= 0 || ySurface >= _worldheight - 15) continue;

                    pos.Set(chunkX * _chunksize + dx, ySurface, chunkZ * _chunksize + dz);

                    if (struc.TryGenerate(_worldgenBlockAccessor, _api.World, pos,
                        climateUpLeft, climateUpRight, climateBotLeft, climateBotRight))
                    {
                        Cuboidi loc = struc.LastPlacedSchematicLocation;

                        string code = struc.Code + (struc.LastPlacedSchematic == null ?
                            "" : "/" + struc.LastPlacedSchematic.FromFileName);

                        region.GeneratedStructures.Add(new GeneratedStructure()
                        {
                            Code = code,
                            Group = Constants.TeleportStructureGroup,
                            Location = loc.Clone()
                        });

                        region.DirtyForSaving = true;

                        if (struc.BuildProtected)
                        {
                            _api.World.Claims.Add(new LandClaim()
                            {
                                Areas = new List<Cuboidi>() { loc.Clone() },
                                Description = struc.BuildProtectionDesc,
                                ProtectionLevel = 10,
                                LastKnownOwnerName = struc.BuildProtectionName,
                                AllowUseEveryone = true
                            });
                        }
                    }
                }
            }
        }
    }
}
