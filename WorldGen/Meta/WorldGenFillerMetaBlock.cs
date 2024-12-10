using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class WorldGenFillerMetaBlock : Block
    {
        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldgenRandom, BlockPatchAttributes attributes = null)
        {
            blockAccessor.SetBlock(0, pos);

            string? code = Attributes["worldGenReplace"]?.AsString(null);
            if (code != null)
            {
                Block? block = blockAccessor.GetBlock(new AssetLocation(code));
                if (block != null)
                {
                    blockAccessor.SetBlock(block.Id, pos, BlockLayersAccess.Solid);
                }
            }

            return true;
        }
    }
}
