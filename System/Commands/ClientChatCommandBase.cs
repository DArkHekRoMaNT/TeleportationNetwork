using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace TeleportationNetwork
{
    public class ClientChatCommandBase
    {
        protected ICoreClientAPI Api { get; }
        protected CommandArgumentParsers Parsers => Api.ChatCommands.Parsers;

        public ClientChatCommandBase(ICoreClientAPI api)
        {
            Api = api;
        }
    }
}
