using CommonLib.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class Core : ModSystem
    {
        /// <summary>
        /// Common ModLogger. For blocks, items, ui and other.
        /// In other mod system use inner Mod.Logger instead
        /// </summary>
        public static ILogger ModLogger { get; private set; } = null!;
        public static Config Config { get; private set; } = null!;

        public HudCircleRenderer? HudCircleRenderer { get; private set; }

        public override void StartPre(ICoreAPI api)
        {
            ModLogger = Mod.Logger;
            var configManager = api.ModLoader.GetModSystem<ConfigManager>();
            Config = configManager.GetConfig<Config>();
        }

        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockBrokenTeleport", typeof(BlockTeleport));
            api.RegisterBlockClass("BlockNormalTeleport", typeof(BlockTeleport));
            api.RegisterBlockEntityClass("BETeleport", typeof(BETeleport));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.RegisterCommand(new OpenTeleportDialogCommand(api));

            HudCircleRenderer = new HudCircleRenderer(api, new HudCircleSettings()
            {
                Color = 0x23cca2
            });
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.RegisterCommand(new ImportSchematicCommand());
            api.RegisterCommand(new RandomTeleportCommand());
            api.RegisterCommand(new RestoreStabilityCommand());
        }
    }
}
