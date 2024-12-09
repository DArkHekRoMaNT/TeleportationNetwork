using Vintagestory.API.Common;

namespace TeleportationNetwork
{
    public record GateSettings(float Rotation, float Size, float Thick, GateShape Shapes);
    public record GateShape(AssetLocation Static, AssetLocation Dynamic, AssetLocation Rod);
}
