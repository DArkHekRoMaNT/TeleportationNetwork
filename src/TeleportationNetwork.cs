using TeleportationNetwork;
using Vintagestory.API.Common;

[assembly: ModInfo(Constants.MOD_ID)]

namespace TeleportationNetwork
{
    public class TeleportationNetwork : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterBlockClass("BlockTeleport", typeof(BlockTeleport));
            api.RegisterBlockEntityClass("BlockEntityTeleport", typeof(BlockEntityTeleport));
        }
    }
}