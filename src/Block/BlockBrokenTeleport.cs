using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class BlockBrokenTeleport : BlockTeleport
    {
        protected override void InitWorldInteractions()
        {
            var temporalGear = new ItemStack(api.World.GetItem(new AssetLocation("gear-temporal")), 1);
            var frames = api.World.Blocks
                        .Where((b) => b.DrawType == EnumDrawType.Cube)
                        .Select((Block b) => new ItemStack(b))
                        .ToArray();

            WorldInteractions = new WorldInteraction[]{
                new WorldInteraction(){
                    ActionLangCode = "blockhelp-translocator-repair-2",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = new ItemStack[] { temporalGear },
                },
                new WorldInteraction()
                {
                    ActionLangCode = Core.ModId + ":blockhelp-teleport-change-frame",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sprint",
                    Itemstacks = frames
                }
            };
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BETeleport be)
            {
                ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (!activeSlot.Empty && activeSlot.Itemstack.Collectible is ItemTemporalGear)
                {
                    Block newBlock = world.GetBlock(CodeWithVariant("state", "normal"));
                    world.BlockAccessor.SetBlock(newBlock.BlockId, blockSel.Position);

                    if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BETeleport newBE)
                    {
                        newBE.FrameStack = be.FrameStack.Clone();
                        newBE.UpdateFrameMesh();
                        newBE.MarkDirty(true);
                    }

                    if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        activeSlot.TakeOut(1);
                        activeSlot.MarkDirty();
                    }

                    if (api.Side == EnumAppSide.Server)
                    {
                        ITeleport teleport = be.TeleportManager.GetTeleport(be.Pos);
                        be.TeleportManager.ActivateTeleport(teleport, byPlayer);
                    }

                    world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position.X + 0.5, blockSel.Position.Y, blockSel.Position.Z + 0.5, byPlayer, true, 16);

                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}