using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class ServerChatCommandBase(ICoreServerAPI api)
    {
        protected ICoreServerAPI Api { get; } = api;
        protected CommandArgumentParsers Parsers => Api.ChatCommands.Parsers;
    }
}
