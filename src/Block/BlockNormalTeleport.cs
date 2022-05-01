using System.Linq;
using SharedUtils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class BlockNormalTeleport : BlockTeleport
    {
        protected override void InitWorldInteractions()
        {
            var frames = api.World.Blocks
                        .Where((b) => b.DrawType == EnumDrawType.Cube)
                        .Select((Block b) => new ItemStack(b))
                        .ToArray();

            WorldInteractions = new WorldInteraction[]{
                new WorldInteraction()
                {
                    ActionLangCode = ConstantsCore.ModId + ":blockhelp-teleport-rename",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sneak"
                },
                new WorldInteraction()
                {
                    ActionLangCode = ConstantsCore.ModId + ":blockhelp-teleport-change-frame",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sprint",
                    Itemstacks = frames
                }
            };
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool flag = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (api.Side == EnumAppSide.Server)
            {
                if (flag && byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
                {
                    if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BETeleport be)
                    {
                        be.TeleportsManager.AddAvailableTeleport(byPlayer as IServerPlayer, blockSel.Position);
                    }
                }
            }

            return flag;
        }
    }
}