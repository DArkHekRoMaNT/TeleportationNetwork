using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;

namespace TeleportationNetwork.Generation.v2
{
    [HarmonyPatch(typeof(BlockSchematicStructure))]
    public static class Patch_BlockSchematicStructure
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BlockSchematicStructure), nameof(BlockSchematicStructure.PlaceRespectingBlockLayers));
            yield return AccessTools.Method(typeof(BlockSchematicStructure), nameof(BlockSchematicStructure.PlaceReplacingBlocks));
        }

        public record State(Dictionary<int, Dictionary<int, int>> ReplaceBlocksCopy, StructureRandomizerInstance Randomizer);

        public static void Prefix(ref BlockSchematicStructure __instance, IWorldAccessor worldForCollectibleResolve, IBlockAccessor blockAccessor, BlockPos startPos,
            ref Dictionary<int, Dictionary<int, int>> replaceBlocks, ref State __state)
        {
            if (replaceBlocks == null)
                return;

            if (!__instance.BlockCodes.Any(x => x.Value.Domain == Constants.ModId)) // Skip generator
                return;

            var replaceBlocksCopy = new Dictionary<int, Dictionary<int, int>>();
            lock (__instance)
            {
                foreach (var dict in replaceBlocks)
                {
                    var innerCopy = new Dictionary<int, int>();
                    foreach (var value in dict.Value)
                    {
                        innerCopy.Add(value.Key, value.Value);
                    }
                    replaceBlocksCopy.Add(dict.Key, innerCopy);
                }
            }

            var centerPos = new BlockPos(__instance.SizeX / 2 + startPos.X, startPos.Y, __instance.SizeZ / 2 + startPos.Z, startPos.dimension);
            var mapChunkAtBlockPos = blockAccessor.GetMapChunkAtBlockPos(centerPos);
            var rockId = mapChunkAtBlockPos.TopRockIdMap[centerPos.Z % 32 * 32 + centerPos.X % 32];
            var randomizer = StructureRandomizer.GetRandomizer(worldForCollectibleResolve, startPos);

            foreach (var blockId in __instance.BlockIds)
            {
                var newBlockId = randomizer.Next(blockId, worldForCollectibleResolve);
                if (newBlockId != blockId)
                {
                    if (replaceBlocks.TryGetValue(blockId, out var value))
                    {
                        value[rockId] = newBlockId;
                    }
                    else
                    {
                        replaceBlocks.Add(blockId, new() { [rockId] = newBlockId });
                    }
                }
            }

            __state = new State(replaceBlocksCopy, randomizer);
        }

        public static void Postfix(ref BlockSchematicStructure __instance, out Dictionary<int, Dictionary<int, int>> replaceBlocks, IBlockAccessor blockAccessor, BlockPos startPos, State __state)
        {
            if (__state == null)
            {
                replaceBlocks = null!;
                return;
            }

            __state.Randomizer.AfterPlace(blockAccessor, startPos, __instance);

            lock (__instance)
            {
                replaceBlocks = __state.ReplaceBlocksCopy;
            }
        }
    }
}
