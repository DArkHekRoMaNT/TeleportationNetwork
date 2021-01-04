// using System;
// using Vintagestory.API.Common;
// using Vintagestory.API.Datastructures;
// using Vintagestory.API.MathTools;
// using Vintagestory.API.Server;
// using Vintagestory.API.Util;
// using Vintagestory.ServerMods;

// namespace TeleportationNetwork
// {
//     public class WorldGen : ModStdWorldGen
//     {

//         #region

//         ICoreServerAPI api;

//         int worldheight;
//         int chunkMapSizeY;
//         int regionChunkSize;

//         ushort[] heightmap;

//         LCGRandom strucRand; // Deterministic random

//         IWorldGenBlockAccessor worldgenBlockAccessor;

//         WorldGenStructure[] shuffledStructures;

//         public override double ExecuteOrder() { return 0.3; }

//         public override void StartServerSide(ICoreServerAPI api)
//         {
//             this.api = api;
//             base.StartServerSide(api);

//             if (DoDecorationPass)
//             {
//                 api.Event.InitWorldGenerator(initWorldGen, "standard");
//                 api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.TerrainFeatures, "standard");
//                 api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);

//                 api.ModLoader.GetModSystem<GenStructuresPosPass>().handler = OnChunkColumnGenPostPass;
//             }
//         }

//         private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
//         {
//             worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
//         }

//         #endregion

//         internal void initWorldGen()
//         {
//             chunksize = api.WorldManager.ChunkSize;
//             worldheight = api.WorldManager.MapSizeY;
//             chunkMapSizeY = api.WorldManager.MapSizeY / chunksize;
//             regionChunkSize = api.WorldManager.RegionSize / chunksize;

//             strucRand = new LCGRandom(api.WorldManager.Seed + 1090);

//             shuffledStructures = new WorldGenStructure[1]; //LOOK Structures size
//         }

//         private void OnChunkColumnGenPostPass(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
//         {
//             IMapRegion region = chunks[0].MapChunk.MapRegion;

//             DoGenStructures(region, chunkX, chunkZ, true, chunkGenParams);
//         }

//         private void OnChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
//         {
//             IMapRegion region = chunks[0].MapChunk.MapRegion;

//             heightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;

//             DoGenStructures(region, chunkX, chunkZ, false, chunkGenParams);
//         }

//         const ushort RANGE = 64;
//         const ushort EPS = 32;
//         private void DoGenStructures(IMapRegion region, int chunkX, int chunkZ, bool postPass, ITreeAttribute chunkGenParams = null)
//         {
//             BlockPos pos = new BlockPos();

//             int partX = chunkX * chunksize / RANGE;
//             int partZ = chunkZ * chunksize / RANGE;

//             strucRand.InitPositionSeed(partX, partZ);

//             int placeX = partX * RANGE + strucRand.NextInt(EPS);
//             int placeZ = partZ * RANGE + strucRand.NextInt(EPS);

//             // Not in this chunk
//             if (!(Math.Abs(chunkX * chunksize - placeX) < chunksize && Math.Abs(chunkZ * chunksize - placeZ) < chunksize)) return;

//             shuffledStructures.Shuffle(strucRand);

//             for (int i = 0; i < shuffledStructures.Length; i++)
//             {
//                 WorldGenStructure struc = shuffledStructures[i];
//                 if (struc.PostPass != postPass) continue;

//                 float chance = struc.Chance;
//                 int toGenerate = 9999;

//                 while (chance-- > strucRand.NextDouble() && toGenerate > 0)
//                 {
//                     int dx = strucRand.NextInt(chunksize);
//                     int dz = strucRand.NextInt(chunksize);
//                     int ySurface = heightmap[dz * chunksize + dx];
//                     if (ySurface <= 0 || ySurface >= worldheight - 15) continue;

//                     if (struc.Placement == EnumStructurePlacement.Underground)
//                     {
//                         if (struc.Depth != null)
//                         {
//                             pos.Set(chunkX * chunksize + dx, ySurface - (int)struc.Depth.nextFloat(1, strucRand), chunkZ * chunksize + dz);
//                         }
//                         else
//                         {
//                             pos.Set(chunkX * chunksize + dx, 8 + strucRand.NextInt(ySurface - 8 - 5), chunkZ * chunksize + dz);
//                         }

//                     }
//                     else
//                     {
//                         pos.Set(chunkX * chunksize + dx, ySurface, chunkZ * chunksize + dz);
//                     }

//                     if (struc.TryGenerate(worldgenBlockAccessor, api.World, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight))
//                     {
//                         Cuboidi loc = struc.LastPlacedSchematicLocation;

//                         string code = struc.Code + (struc.LastPlacedSchematic == null ? "" : "/" + struc.LastPlacedSchematic.FromFileName);

//                         region.GeneratedStructures.Add(new GeneratedStructure() { Code = code, Group = struc.Group, Location = loc.Clone() });
//                         region.DirtyForSaving = true;

//                         if (struc.BuildProtected)
//                         {
//                             api.World.Claims.Add(new LandClaim()
//                             {
//                                 Areas = new List<Cuboidi>() { loc.Clone() },
//                                 Description = struc.BuildProtectionDesc,
//                                 ProtectionLevel = 10,
//                                 LastKnownOwnerName = struc.BuildProtectionName,
//                                 AllowUseEveryone = true
//                             });
//                         }

//                         toGenerate--;
//                     }
//                 }
//             }

//         }
//     }
// }