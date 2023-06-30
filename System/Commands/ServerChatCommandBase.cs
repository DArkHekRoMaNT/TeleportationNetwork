using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class ServerChatCommandBase
    {
        protected ICoreServerAPI Api { get; }
        protected CommandArgumentParsers Parsers => Api.ChatCommands.Parsers;

        public ServerChatCommandBase(ICoreServerAPI api)
        {
            Api = api;
        }
    }
}
