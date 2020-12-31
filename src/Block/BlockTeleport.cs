using Vintagestory.API.Common;

namespace TeleportationNetwork
{
    public class BlockTeleport : Block
    {
        public Shape GetShape(string type = "core")
        {
            string path = "shapes/block/teleport/" + type + ".json";
            return api.Assets.Get(new AssetLocation(Constants.MOD_ID, path)).ToObject<Shape>();
        }
    }
}