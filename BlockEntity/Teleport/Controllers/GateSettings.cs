using Vintagestory.API.Common;

namespace TeleportationNetwork
{
    public record GateSettings(float Rotation, float Size, float Thick,
                               (AssetLocation Static, AssetLocation Dynamic, AssetLocation Rod) Shapes);
}
