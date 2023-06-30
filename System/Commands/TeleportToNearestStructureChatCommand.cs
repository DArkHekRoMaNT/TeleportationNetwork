using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class TeleportToNearestStructureChatCommand : ServerChatCommandBase
    {
        public TeleportToNearestStructureChatCommand(ICoreServerAPI api) : base(api)
        {
            api.ChatCommands
                .GetOrCreate("tpnet")
                .BeginSubCommand("nexttp")
                    .RequiresPrivilege(Privilege.tp)
                    .WithDescription("Teleport player to nearest teleport structure")
                    .RequiresPlayer()
                    .HandleWith(TeleportPlayerTo)
                .EndSubCommand();
        }
        private TextCommandResult TeleportPlayerTo(TextCommandCallingArgs args)
        {
            int chunkSize = Api.World.BlockAccessor.ChunkSize;
            int chunkX = args.Caller.Pos.XInt / chunkSize;
            int chunkZ = args.Caller.Pos.ZInt / chunkSize;

            var teleportGenerator = Api.ModLoader.GetModSystem<GenTeleportStructures>();
            BlockPos pos = teleportGenerator.GetTeleportPosHere(chunkX, chunkZ);

            args.Caller.Entity.TeleportTo(pos);
            Api.WorldManager.LoadChunkColumnPriority(pos.X / chunkSize, pos.Z / chunkSize, new ChunkLoadOptions
            {
                OnLoaded = () =>
                {
                    pos.Y = Api.WorldManager.GetSurfacePosY(pos.X, pos.Z) ?? Api.WorldManager.MapSizeY;
                    args.Caller.Entity.TeleportTo(pos);
                }
            });

            return TextCommandResult.Success();
        }
    }
}
