using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class RestoreStabilityCommand : ServerChatCommand
    {
        public RestoreStabilityCommand()
        {
            Command = "rst";
            Description = Core.ModPrefix + "Restore player temporal stability";
            Syntax = "/rst [player]";
            RequiredPrivilege = Privilege.commandplayer;
            handler = (player, groupId, args) =>
            {
                var targetPlayer = player;

                string? playerName = args?.PopWord();
                if (playerName != null)
                {
                    foreach (var p in player.Entity.Api.World.AllOnlinePlayers)
                    {
                        if (p.PlayerName.ToLower() == playerName.ToLower())
                        {
                            targetPlayer = player;
                            break;
                        }
                    }
                }

                targetPlayer.Entity.WatchedAttributes.SetDouble("temporalStability", 1);
            };
        }
    }
}
