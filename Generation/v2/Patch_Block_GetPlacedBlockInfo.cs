using HarmonyLib;
using Vintagestory.API.Common;

namespace TeleportationNetwork.Generation.v2 //TOOD: Move to CommonLib?
{
    [HarmonyPatch(typeof(Block))]
    [HarmonyPatch(nameof(Block.GetPlacedBlockInfo))]
    public static class Patch_Block_GetPlacedBlockInfo
    {
        public static void Postfix(ref Block __instance, ref string __result)
        {
            __result = $"{__instance.Code}\n{__result}";
        }
    }
}
