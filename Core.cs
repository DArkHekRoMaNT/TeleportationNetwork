using CommonLib.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class Core : ModSystem
    {
        public static Config Config { get; private set; } = null!;

        public HudCircleRenderer? HudCircleRenderer { get; private set; }

        public override void StartPre(ICoreAPI api)
        {
            var configManager = api.ModLoader.GetModSystem<ConfigManager>();
            Config = configManager.GetConfig<Config>();
        }

        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockBrokenTeleport", typeof(BlockTeleport));
            api.RegisterBlockClass("BlockNormalTeleport", typeof(BlockTeleport));
            api.RegisterBlockEntityClass("BETeleport", typeof(BETeleport));

            api.ChatCommands
                .GetOrCreate("tpnet")
                .WithDescription("Teleportation Network commands")
                .RequiresPrivilege(Privilege.chat);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            HudCircleRenderer = new HudCircleRenderer(api, new HudCircleSettings()
            {
                Color = 0x23cca2
            });

            _ = new OpenTeleportDialogChatCommand(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            _ = new SchematicChatCommand(api);
        }
    }
}
