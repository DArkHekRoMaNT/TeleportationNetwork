using System.Linq;
using HarmonyLib;
using SharedUtils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class BlockTeleport : Block
    {
        private WorldInteraction[] WorldInteractions_Normal;
        private WorldInteraction[] WorldInteractions_Broken;

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (!(api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityTeleport be)) return;

            be.OnEntityCollide(entity);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!(api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityTeleport be))
            {
                //api.World.BlockAccessor.SetBlock(BlockId, blockSel.Position); //TODO Check it
                return false;
            }

            if (be.IsNormal && byPlayer.WorldData.EntityControls.Sneak)
            {
                be.OpenRenameDlg();
                return true;
            }

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            if (!be.IsNormal && slot.Itemstack.Collectible is ItemTemporalGear)
            {
                be.State = TeleportState.Normal;

                if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    slot.TakeOut(1);
                    slot.MarkDirty();
                }
                if (api.Side == EnumAppSide.Server)
                {
                    be.TPNetManager.AddAvailableTeleport(byPlayer as IServerPlayer, be.Pos);
                }

                world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position.X + 0.5, blockSel.Position.Y, blockSel.Position.Z + 0.5, byPlayer, true, 16);

                return true;
            }

            if (byPlayer.Entity.Controls.Sprint &&
                slot.Itemstack.Class == EnumItemClass.Block &&
                slot.Itemstack.Block.DrawType == EnumDrawType.Cube &&
                !slot.Itemstack.Collectible.Equals(slot.Itemstack, be.FrameStack))
            {
                be.FrameStack = slot.TakeOut(1);
                return true;
            }

            return false;
        }

        //LOOK OnAsyncClientParticleTick
        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            base.OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool flag = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (flag && byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityTeleport bet
                    && bet.State == TeleportState.Normal && api.Side == EnumAppSide.Server)
                {
                    bet.TPNetManager.AddAvailableTeleport(byPlayer as IServerPlayer, blockSel.Position);
                }
            }

            return flag;
        }

        // TODO: Need over way for prevent broke
        // TODO: Need check
        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            if (Config.Current.Unbreakable.Val) remainingResistance = 1f;

            return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (LastCodePart() == TeleportState.Broken)
            {
                return WorldInteractions_Broken ?? (WorldInteractions_Broken = new WorldInteraction[]{
                    new WorldInteraction(){
                        ActionLangCode = "blockhelp-translocator-repair-2",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = new ItemStack[] { new ItemStack(world.GetItem(new AssetLocation("gear-temporal")), 1) },
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = ConstantsCore.ModId + ":blockhelp-teleport-change-frame",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak",
                        Itemstacks = api.World.Blocks.Where((b)=>b.DrawType == EnumDrawType.Cube).Select((Block b)=>new ItemStack(b)).ToArray()
                    }
                });
            }
            if (LastCodePart() == TeleportState.Normal)
            {
                return WorldInteractions_Normal ?? (WorldInteractions_Normal = new WorldInteraction[]{
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
                        HotKeyCode = "sneak",
                        Itemstacks = api.World.Blocks.Where((b)=>b.DrawType == EnumDrawType.Cube).Select((Block b)=>new ItemStack(b)).ToArray()
                    }
                });
            }
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            var drops = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier) ?? new ItemStack[] { };
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityTeleport bet)
            {
                if (bet.FrameStack.Collectible.Code != bet.DefaultFrameCode)
                {
                    drops.AddItem(bet.FrameStack);
                }
            }
            return drops;
        }
    }
}