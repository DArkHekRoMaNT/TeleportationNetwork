using SharedUtils;
using SharedUtils.Extensions;
using SharedUtils.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace TeleportationNetwork
{
    public class Core : ModSystem
    {
        public HudCircleRenderer HudCircleRenderer { get; private set; }
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockTeleport", typeof(BlockTeleport));
            api.RegisterBlockEntityClass("BETeleport", typeof(BlockEntityTeleport));

            TreeAttribute.RegisterAttribute(Constants.ATTRIBUTES_ID + 1, typeof(BlockPosArrayAttribute));

            Config.Current = api.LoadOrCreateConfig<Config>(ConstantsCore.ModId + ".json");
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            HudCircleRenderer = new HudCircleRenderer(api, new HudCircleSettings() { Color = 0x23cca2 });
        }
    }
}