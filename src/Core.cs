using SharedUtils.Extensions;
using SharedUtils.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace TeleportationNetwork
{
    public class Core : ModSystem
    {
        public Core() => Instance = this;
        public static Core Instance { get; private set; }

        public static ILogger ModLogger => Instance.Mod.Logger;
        public static string ModId => Instance.Mod.Info.ModID;
        public static string ModPrefix => $"[{ModId}] ";


        public HudCircleRenderer HudCircleRenderer { get; private set; }

        private ICoreAPI api;

        public override void StartPre(ICoreAPI api)
        {
            Config.Current = api.LoadOrCreateConfig<Config>(ModId + ".json");
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;

            ClassRegister();

            TreeAttribute.RegisterAttribute(Constants.AttributesId + 1, typeof(BlockPosArrayAttribute));
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

        public override void Dispose()
        {
            base.Dispose();
            Instance = null;
        }
    }
}