using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class GuiElementTeleportButton(
        ICoreClientAPI capi,
        string text,
        CairoFont font,
        CairoFont hoverFont,
        ActionConsumable onClick,
        ElementBounds bounds,
        EnumButtonStyle style = EnumButtonStyle.Normal) :
        GuiElementTextButton(capi, text, font, hoverFont, onClick, bounds, style)
    {
        public required BlockPos TeleportPos { get; set; }
    }
}
