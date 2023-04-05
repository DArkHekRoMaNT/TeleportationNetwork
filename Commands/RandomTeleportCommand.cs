using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class RandomTeleportCommand : ServerChatCommand
    {
        public RandomTeleportCommand()
        {
            Command = "rndtp";
            Description = "[" + Constants.ModId + "] Teleport player to random location";
            Syntax = "/rndtp [range]";
            RequiredPrivilege = Privilege.tp;
            handler = (player, groupId, args) =>
            {
                TeleportUtil.RandomTeleport(player, logger, args.PopInt() ?? -1);
            };
        }
    }
}
