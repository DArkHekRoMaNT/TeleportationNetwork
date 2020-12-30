using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class GuiDialogTeleport : GuiDialogBlockEntity
    {
        public GuiDialogTeleport(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi) : base(dialogTitle, inventory, blockEntityPos, capi)
        {
            double pad = GuiElementItemSlotGrid.unscaledSlotPadding;
            int spacing = 5;
            double size = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGrid.unscaledSlotPadding;
            double innerWidth = 300;


        }
    }
}