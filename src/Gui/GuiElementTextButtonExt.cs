using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class GuiElementTextButtonExt : GuiElementTextButton
    {
        public BlockPos TeleportPos { get; private set; }

        public GuiElementTextButtonExt(ICoreClientAPI capi, string text, BlockPos teleportPos, CairoFont font, CairoFont hoverFont, ActionConsumable onClick, ElementBounds bounds, EnumButtonStyle style = EnumButtonStyle.Normal) : base(capi, text, font, hoverFont, onClick, bounds, style)
        {
            this.TeleportPos = teleportPos;
        }
    }
}