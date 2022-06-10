using HarmonyLib;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace TeleportationNetwork
{

    public abstract class BlockTeleport : Block
    {
        protected WorldInteraction[] WorldInteractions { get; set; }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            InitWorldInteractions();
        }

        protected abstract void InitWorldInteractions();

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (api.World.BlockAccessor.GetBlockEntity(pos) is BETeleport be)
            {
                be.OnEntityCollide(entity);
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BETeleport be)
            {
                if (byPlayer.WorldData.EntityControls.Sneak)
                {
                    be.OpenRenameDlg();
                    return true;
                }

                ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (!activeSlot.Empty)
                {
                    if (byPlayer.Entity.Controls.Sprint &&
                        activeSlot.Itemstack.Class == EnumItemClass.Block &&
                        activeSlot.Itemstack.Block.DrawType == EnumDrawType.Cube &&
                        !activeSlot.Itemstack.Collectible.Equals(activeSlot.Itemstack, be.FrameStack))
                    {
                        api.World.SpawnItemEntity(be.FrameStack, blockSel.Position.ToVec3d().Add(TopMiddlePos));
                        be.FrameStack = activeSlot.TakeOut(1);
                        return true;
                    }
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (Config.Current.Unbreakable.Val && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                return;
            }

            if (world.Api.Side == EnumAppSide.Client)
            {
                var core = world.Api.ModLoader.GetModSystem<Core>();
                core.HudCircleRenderer.CircleVisible = false;
            }
            else
            {
                var teleportManager = world.Api.ModLoader.GetModSystem<TeleportSystem>().Manager;
                ITeleport teleport = teleportManager.GetTeleport(pos);
                if (teleport != null)
                {
                    teleportManager.RemoveTeleport(teleport);
                }
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            var drops = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier) ?? Array.Empty<ItemStack>();
            if (world.BlockAccessor.GetBlockEntity(pos) is BETeleport bet)
            {
                if (bet.FrameStack.Collectible.Code != BETeleport.DefaultFrameCode)
                {
                    drops.AddItem(bet.FrameStack);
                }
            }
            return drops;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer)
                .Append(WorldInteractions);
        }
    }
}