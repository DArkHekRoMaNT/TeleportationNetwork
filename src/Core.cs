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
        public static string ModId { get; private set; }
        public static ILogger ModLogger { get; private set; }

        public HudCircleRenderer HudCircleRenderer { get; private set; }

        ICoreAPI api;

        public override void Start(ICoreAPI api)
        {
            this.api = api;

            ClassRegister();

            TreeAttribute.RegisterAttribute(Constants.ATTRIBUTES_ID + 1, typeof(BlockPosArrayAttribute));

            Config.Current = api.LoadOrCreateConfig<Config>(ConstantsCore.ModId + ".json");
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            HudCircleRenderer = new HudCircleRenderer(api, new HudCircleSettings() { Color = 0x23cca2 });
        }

        private void ClassRegister()
        {
            api.RegisterBlockClass("BlockBrokenTeleport", typeof(BlockBrokenTeleport));
            api.RegisterBlockClass("BlockNormalTeleport", typeof(BlockNormalTeleport));
            api.RegisterBlockEntityClass("BETeleport", typeof(BETeleport));
        }

    }
}