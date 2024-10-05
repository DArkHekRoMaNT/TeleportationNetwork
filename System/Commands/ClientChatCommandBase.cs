using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace TeleportationNetwork
{
    public class ClientChatCommandBase(ICoreClientAPI api)
    {
        protected ICoreClientAPI Api { get; } = api;
        protected CommandArgumentParsers Parsers => Api.ChatCommands.Parsers;
    }
}
