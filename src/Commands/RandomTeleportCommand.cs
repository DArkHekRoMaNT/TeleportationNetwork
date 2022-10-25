using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class RandomTeleportCommand : ServerChatCommand
    {
        public RandomTeleportCommand()
        {
            Command = "rndtp";
            Description = Core.ModPrefix + "Teleport player to random location";
            Syntax = "/rndtp [range]";
            RequiredPrivilege = Privilege.tp;
            handler = (player, groupId, args) =>
            {
                TeleportUtil.RandomTeleport(player, args.PopInt() ?? -1);
            };
        }
    }
}
