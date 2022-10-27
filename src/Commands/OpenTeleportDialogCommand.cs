using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace TeleportationNetwork
{
    public class OpenTeleportDialogCommand : ClientChatCommand
    {
        public OpenTeleportDialogCommand(ICoreClientAPI api)
        {
            Command = "tpdlg";
            Description = Core.ModPrefix + "Open teleport dialog (creative only)";
            Syntax = ".tpdlg";

            handler = (groupId, args) =>
            {
                if (api.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
                {
                    GuiDialogTeleportList dialog = new(api, null);
                    dialog.TryOpen();
                }
            };
        }
    }
}
