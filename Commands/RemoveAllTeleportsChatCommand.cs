using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class RemoveAllTeleportsChatCommand : ServerChatCommandBase
    {
        private bool _accepted = false;
        private IPlayer? _latestPlayer;

        public RemoveAllTeleportsChatCommand(ICoreServerAPI api) : base(api)
        {
            api.ChatCommands
                .GetOrCreate("tpnet")
                .BeginSubCommand("removeall")
                    .WithDescription("Remove all teleports")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(RemoveAll)
                .EndSubCommand();
        }

        private TextCommandResult RemoveAll(TextCommandCallingArgs args)
        {
            if (_accepted && _latestPlayer == args.Caller.Player)
            {
                _accepted = false;
                var manager = Api.ModLoader.GetModSystem<TeleportManager>();
                foreach (var teleport in manager.Points)
                {
                    int chunkX = teleport.Pos.X / Api.World.BlockAccessor.ChunkSize;
                    int chunkZ = teleport.Pos.Z / Api.World.BlockAccessor.ChunkSize;

                    Api.WorldManager.LoadChunkColumnPriority(chunkX, chunkZ, new ChunkLoadOptions
                    {
                        OnLoaded = () =>
                        {
                            Api.World.BlockAccessor.SetBlock(0, teleport.Pos);
                        }
                    });
                }

                return TextCommandResult.Success("Removing started");
            }

            _latestPlayer = args.Caller.Player;
            _accepted = true;
            return TextCommandResult.Success("<font color=#ffaaaa>Warning!</font> This will remove all existing teleport blocks. " +
                "It cannot be undone. If you are sure, enter the command again");
        }
    }
}
