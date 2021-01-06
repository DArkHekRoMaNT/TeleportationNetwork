using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public static class TreeAttributeExtensions
    {
        public static BlockPos[] GetBlockPosArray(this TreeAttribute tree, string key, BlockPos[] defaultValue = null)
        {
            return (tree[key] as BlockPosArrayAttribute)?.value ?? defaultValue;
        }
        public static void SetBlockPosArray(this TreeAttribute tree, string key, BlockPos[] value)
        {
            lock (tree.attributesLock)
            {
                tree[key] = new BlockPosArrayAttribute(value);
            }
        }
    }
}