using SharedUtils;
using SharedUtils.Extensions;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace TeleportationNetwork
{
    public class Core : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockTeleport", typeof(BlockTeleport));
            api.RegisterBlockEntityClass("BETeleport", typeof(BlockEntityTeleport));

            TreeAttribute.RegisterAttribute(Constants.ATTRIBUTES_ID + 1, typeof(BlockPosArrayAttribute));

            Config.Current = api.LoadOrCreateConfig<Config>(ConstantsCore.ModId + ".json");
        }
    }
}