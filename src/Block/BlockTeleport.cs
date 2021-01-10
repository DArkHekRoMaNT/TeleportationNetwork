using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class BlockTeleport : Block
    {
        //public MeshData[] meshes = new MeshData[2];

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            BETeleport be = api.World.BlockAccessor.GetBlockEntity(pos) as BETeleport;
            if (be == null) return;

            be.OnEntityCollide(entity);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BETeleport be = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BETeleport;
            if (be == null) return false;

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            if (!be.Repaired && slot.Itemstack.Collectible is ItemTemporalGear)
            {
                be.Repaired = true;

                if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    slot.TakeOut(1);
                    slot.MarkDirty();
                }

                TPNetManager.AddAvailableTeleport(byPlayer, be.Pos);

                world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position.X + 0.5, blockSel.Position.Y, blockSel.Position.Z + 0.5, byPlayer, true, 16);

                return true;
            }

            return false;
        }

        //LOOK OnAsyncClientParticleTick
        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            base.OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            base.OnBlockRemoved(world, pos);
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool flag = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (flag && byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                BETeleport bet = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BETeleport;

                if (bet != null && bet.State == EnumTeleportState.Normal)
                {
                    TPNetManager.AddAvailableTeleport(byPlayer, blockSel.Position);
                }
            }

            return flag;
        }

        // TOFO: Need over way for prevent broke
        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            if (Config.Current.Unbreakable.Val) return 9999;

            return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (LastCodePart() == "broken")
            {
                return new WorldInteraction[]{
                    new WorldInteraction(){
                        ActionLangCode = "blockhelp-translocator-repair-2",
                        Itemstacks = new ItemStack[] { new ItemStack(world.GetItem(new AssetLocation("gear-temporal")), 1) },
                        MouseButton = EnumMouseButton.Right
                    }
                };
            }
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }
    }
}