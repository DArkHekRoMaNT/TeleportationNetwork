using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class OpenTeleportDialogChatCommand : ClientChatCommandBase
    {
        public OpenTeleportDialogChatCommand(ICoreClientAPI api) : base(api)
        {
            api.ChatCommands
                .GetOrCreate("tpnet")
                .BeginSubCommand("dialog")
                    .WithDescription("Open teleport dialog")
                    .RequiresPrivilege(Privilege.tp)
                    .HandleWith(OpenTeleportDialog)
                .EndSubCommand();
        }

        private TextCommandResult OpenTeleportDialog(TextCommandCallingArgs args)
        {
            var dialog = new GuiDialogTeleportList(Api, null);
            dialog.TryOpen();
            return TextCommandResult.Success();
        }
    }
}
