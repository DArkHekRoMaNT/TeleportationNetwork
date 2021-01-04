// using System;
// using System.Collections.Generic;
// using Vintagestory.API.Common;
// using Vintagestory.API.Datastructures;
// using Vintagestory.API.MathTools;
// using Vintagestory.API.Server;

// namespace TeleportationNetwork
// {
//     public class WorldGen : ModSystem
//     {
//         private ICoreServerAPI api;
//         IWorldGenBlockAccessor blockAccessor;
//         int chunkSize;

//         LCGRandom random;
//         const ushort RANGE = 64;
//         const ushort EPS = 32;

//         public override double ExecuteOrder() => 0.3;
//         public override bool ShouldLoad(EnumAppSide side)
//         {
//             return side == EnumAppSide.Server;
//         }

//         public override void StartServerSide(ICoreServerAPI api)
//         {
//             this.api = api;
//             this.chunkSize = this.api.World.BlockAccessor.ChunkSize;
//             this.random = new LCGRandom(api.World.Seed);

//             this.api.Event.ChunkColumnGeneration(OnChunkColumnGeneration, EnumWorldGenPass.TerrainFeatures, "standard");
//             this.api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
//         }

//         private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
//         {
//             blockAccessor = chunkProvider.GetBlockAccessor(false);
//         }

//         private Vec3i ShouldGenerateTeleport(int chunkX, int chunkZ)
//         {
//             int partX = chunkX * chunkSize / RANGE;
//             int partZ = chunkZ * chunkSize / RANGE;

//             random.InitPositionSeed(partX, partZ);

//             int placeX = partX * RANGE + random.NextInt(EPS);
//             int placeZ = partZ * RANGE + random.NextInt(EPS);

//             if (Math.Abs(chunkX * chunkSize - placeX) < chunkSize &&
//                 Math.Abs(chunkZ * chunkSize - placeZ) < chunkSize)
//             {
//                 return new Vec3i(placeX, 0, placeZ);
//             }
//             return null;
//         }

//         private void OnChunkColumnGeneration(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
//         {
//             Vec3i pos = ShouldGenerateTeleport(chunkX, chunkZ);
//             if (pos == null) return;

//             for (int i = api.WorldManager.MapSizeY - 2; i > 0; i--)
//             {
//                 if (blockAccessor.GetBlockId(pos.X, i, pos.Z) != 0)
//                 {
//                     GenerateTeleport(pos.Add(0, i + 1, 0).AsBlockPos); //LOOK i + 1 ?
//                     return;
//                 }
//             }
//         }

//         public void GenerateTeleport(BlockPos pos)
//         {
//             Block block = api.World.GetBlock(new AssetLocation(Constants.MOD_ID, "teleport-normal"));

//             blockAccessor.SetBlock(0, pos);
//             block.TryPlaceBlockForWorldGen(blockAccessor, pos, BlockFacing.UP, null);

//             api.SendMessageAll("generated at " + pos.Add(-api.WorldManager.MapSizeX / 2, 0, -api.WorldManager.MapSizeZ / 2));
//         }
//         public static void GenerateTeleport(ICoreServerAPI api, BlockPos pos)
//         {
//             Block block = api.World.GetBlock(new AssetLocation(Constants.MOD_ID, "teleport-normal"));
//             api.World.BlockAccessor.SetBlock(block.BlockId, pos);
//         }
//     }
// }