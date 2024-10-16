using CommonLib.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class Core : ModSystem
    {
        public static Config Config { get; private set; } = null!;

        public override void StartPre(ICoreAPI api)
        {
            var configManager = api.ModLoader.GetModSystem<ConfigManager>();
            Config = configManager.GetConfig<Config>();
        }

        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockTeleport", typeof(BlockTeleport));
            api.RegisterBlockEntityClass("BETeleport", typeof(BlockEntityTeleport));

            api.RegisterBlockClass("WorldGenReplaceMetaBlock", typeof(WorldGenFillerMetaBlock));
            api.RegisterBlockEntityClass("Dome", typeof(BlockEntityDome));

            api.ChatCommands
                .GetOrCreate("tpnet")
                .WithDescription("Teleportation Network commands")
                .RequiresPrivilege(Privilege.chat);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            _ = new OpenTeleportDialogChatCommand(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            _ = new SchematicChatCommand(api);
            _ = new RemoveAllTeleportsChatCommand(api);
        }
    }
}
