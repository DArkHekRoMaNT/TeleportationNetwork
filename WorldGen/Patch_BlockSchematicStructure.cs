using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.ServerMods;

namespace TeleportationNetwork.WorldGen
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

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original, ILGenerator generator)
        {
            var isUnderground = original.Name == nameof(BlockSchematicStructure.PlaceReplacingBlocks);
            var replaceBlocksIndex = isUnderground ? 5 : 8;

            var randomizerIndex = generator.DeclareLocal(typeof(StructureRandomizer)).LocalIndex;
            yield return CodeInstruction.LoadArgument(2); // IWorldAccessor
            yield return CodeInstruction.LoadArgument(3); // startPos
            yield return CodeInstruction.LoadArgument(0); // schematic
            yield return CodeInstruction.Call(typeof(StructureRandomizer), nameof(StructureRandomizer.GetRandomizer));
            yield return CodeInstruction.StoreLocal(randomizerIndex);

            var newReplaceBlocksIndex = generator.DeclareLocal(typeof(Dictionary<int, Dictionary<int, int>>)).LocalIndex;
            yield return CodeInstruction.LoadLocal(randomizerIndex);
            yield return CodeInstruction.LoadArgument(replaceBlocksIndex);
            yield return CodeInstruction.LoadArgument(1); // IBlockAccessor
            yield return CodeInstruction.LoadArgument(2); // IWorldAccessor
            if (isUnderground)
            {
                // rockBlockId
                yield return CodeInstruction.LoadArgument(6);
            }
            else
            {
                // Just null o.O
                var tempIndex = generator.DeclareLocal(typeof(int?));
                yield return new CodeInstruction(OpCodes.Ldloca, tempIndex);
                yield return new CodeInstruction(OpCodes.Initobj, typeof(int?));
                yield return new CodeInstruction(OpCodes.Ldloc, tempIndex);
            }
            yield return CodeInstruction.Call(typeof(StructureRandomizerInstance), nameof(StructureRandomizerInstance.GetNewReplaceBlocks));
            yield return CodeInstruction.StoreLocal(newReplaceBlocksIndex);

            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count - 2; i++)
            {
                if (codes[i].IsLdarg(replaceBlocksIndex))
                {
                    var newVar = CodeInstruction.LoadLocal(newReplaceBlocksIndex);
                    newVar.labels = codes[i].labels;
                    newVar.blocks = codes[i].blocks;
                    yield return newVar;
                    continue;
                }
                yield return codes[i];
            }

            yield return CodeInstruction.LoadLocal(randomizerIndex);
            yield return CodeInstruction.LoadArgument(1); // IBlockAccessor
            yield return CodeInstruction.Call(typeof(StructureRandomizerInstance), nameof(StructureRandomizerInstance.AfterPlace));

            yield return codes[^2]; // count
            yield return codes[^1]; // return
        }
    }
}
