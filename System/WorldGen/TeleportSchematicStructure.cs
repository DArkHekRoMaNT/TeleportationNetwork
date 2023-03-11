using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace TeleportationNetwork
{
    public class TeleportSchematicStructure : BlockSchematicStructure
    {
        public delegate void OnBlockEntityPlacedDelegate(int x, int y, int z, IBlockAccessor blockAccessor);
        public OnBlockEntityPlacedDelegate? BlockEntityPlaced { get; set; }

        public int PlaceWithReplaceBlockIds(IBlockAccessor blockAccessor, IWorldAccessor world,
            BlockPos pos, StructureBlockResolver resolver)
        {
            var curPos = new BlockPos();
            int placed = 0;

            PlaceBlockDelegate handler = null!;
            switch (ReplaceMode)
            {
                case EnumReplaceMode.ReplaceAll:
                    handler = PlaceReplaceAll;
                    break;

                case EnumReplaceMode.Replaceable:
                    handler = PlaceReplaceable;
                    break;

                case EnumReplaceMode.ReplaceAllNoAir:
                    handler = PlaceReplaceAllNoAir;
                    break;

                case EnumReplaceMode.ReplaceOnlyAir:
                    handler = PlaceReplaceOnlyAir;
                    break;
            }

            int chunksize = blockAccessor.ChunkSize;
            for (int i = 0; i < Indices.Count; i++)
            {
                uint index = Indices[i];
                int storedBlockid = BlockIds[i];

                int dx = (int)(index & 0x1ff);
                int dy = (int)((index >> 20) & 0x1ff);
                int dz = (int)((index >> 10) & 0x1ff);

                AssetLocation blockCode = BlockCodes[storedBlockid];
                Block? newBlock = resolver.GetBlock(blockCode, blockAccessor);
                if (newBlock == null) continue;

                curPos.Set(dx + pos.X, dy + pos.Y, dz + pos.Z);

                if (newBlock.LightHsv[2] > 0 && blockAccessor is IWorldGenBlockAccessor worldGenBlockAccessor)
                {
                    Block oldBlock = worldGenBlockAccessor.GetBlock(curPos);
                    worldGenBlockAccessor.ScheduleBlockLightUpdate(curPos.Copy(), oldBlock.Id, newBlock.Id);
                }

                int p = handler(blockAccessor, curPos, newBlock, true);

                // In the post pass the rain map does not update, so let's set it ourselves
                if (p > 0 && !newBlock.RainPermeable)
                {
                    IMapChunk mapchunk = blockAccessor.GetMapChunkAtBlockPos(curPos);
                    int lx = curPos.X % chunksize;
                    int lz = curPos.Z % chunksize;
                    int y = mapchunk.RainHeightMap[lz * chunksize + lx];
                    mapchunk.RainHeightMap[lz * chunksize + lx] = (ushort)Math.Max(y, curPos.Y);
                }
            }

            if (blockAccessor is not IBlockAccessorRevertable)
            {
                PlaceEntitiesAndBlockEntities(blockAccessor, world, pos);
                for (int i = 0; i < Indices.Count; i++)
                {
                    uint index = Indices[i];
                    int x = pos.X + (int)(index & 0x1ff);
                    int y = pos.Y + (int)((index >> 20) & 0x1ff);
                    int z = pos.Z + (int)((index >> 10) & 0x1ff);
                    BlockEntityPlaced?.Invoke(x, y, z, blockAccessor);
                }
            }

            return placed;
        }

        public TeleportSchematicStructure Copy()
        {
            return new TeleportSchematicStructure()
            {
                SizeX = SizeX,
                SizeY = SizeY,
                SizeZ = SizeZ,
                BlockCodes = new Dictionary<int, AssetLocation>(BlockCodes),
                ItemCodes = new Dictionary<int, AssetLocation>(ItemCodes),
                Indices = new List<uint>(Indices),
                BlockIds = new List<int>(BlockIds),
                BlockEntities = new Dictionary<uint, string>(BlockEntities),
                ReplaceMode = ReplaceMode,
                FromFileName = FromFileName
            };
        }
    }
}
