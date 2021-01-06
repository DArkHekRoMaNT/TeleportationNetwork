using System.Diagnostics;
using TeleportationNetwork;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

[assembly: ModInfo(Constants.MOD_ID)]

namespace TeleportationNetwork
{
    public class Core : ModSystem
    {
        ICoreAPI api;
        public override void Start(ICoreAPI api)
        {
            RuntimeEnv.DebugOutOfRangeBlockAccess = true;

            api.RegisterBlockClass("BlockTeleport", typeof(BlockTeleport));
            api.RegisterBlockEntityClass("BETeleport", typeof(BETeleport));

            TreeAttribute.RegisterAttribute(Constants.ATTRIBUTES_ID + 1, typeof(BlockPosArrayAttribute));

            Config.Current = api.LoadOrCreateConfig<Config>(Constants.MOD_ID);
        }
    }
}