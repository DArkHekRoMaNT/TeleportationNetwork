using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace CommonLib.Templates //TODO: Move to CommonLib
{
    public abstract class HarmonySystem : ModSystem
    {
        protected readonly string patchCode;
        protected Harmony harmonyInstance = null!;

        public HarmonySystem()
        {
            patchCode = GetType().AssemblyQualifiedName!;
        }

        public HarmonySystem(string patchCode)
        {
            this.patchCode = patchCode;
        }

        public override void StartPre(ICoreAPI api)
        {
            if (api is ICoreClientAPI capi)
            {
                // Prevent double patches in singleplayer
                if (!capi.IsSinglePlayer)
                {
                    PatchAll();
                }
            }
            else
            {
                PatchAll();
            }
        }

        protected virtual void PatchAll()
        {
            harmonyInstance = new Harmony(patchCode);
            harmonyInstance.PatchAll();
            var patchedMethods = harmonyInstance.GetPatchedMethods();
            Mod.Logger.Notification($"Harmony patched:\n{string.Join("\n", patchedMethods)}");
        }

        public override void Dispose()
        {
            harmonyInstance?.UnpatchAll(patchCode);
        }
    }
}
