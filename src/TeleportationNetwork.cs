using Vintagestory.API.Common;

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