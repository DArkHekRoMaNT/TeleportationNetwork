using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{

    public class BlockTeleport : Block
    {
        private List<WorldInteraction> WorldInteractions { get; } = new();

        public bool IsBroken => LastCodePart() == "broken";
        public bool IsNormal => LastCodePart() == "normal";


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            InitWorldInteractions();
        }

        private void InitWorldInteractions()
        {
            if (IsBroken)
            {
                var temporalGear = api.World.GetItem(new AssetLocation("gear-temporal"));

                WorldInteractions.Add(new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-translocator-repair-2",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = new ItemStack[] { new(temporalGear) }
                });
            }

            if (IsNormal)
            {
                WorldInteractions.Add(new WorldInteraction()
                {
                    ActionLangCode = Core.ModId + ":blockhelp-teleport-rename",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sneak"
                });
            }

            var frames = api.World.Blocks
                        .Where((b) => b.DrawType == EnumDrawType.Cube)
                        .Select((Block b) => new ItemStack(b))
                        .ToArray();

            WorldInteractions.Add(new WorldInteraction()
            {
                ActionLangCode = Core.ModId + ":blockhelp-teleport-change-frame",
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "sprint",
                Itemstacks = frames
            });
        }

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
                if (byPlayer.Entity.Controls.Sneak)
                {
                    be.OpenRenameDlg();
                    return true;
                }

                ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (!activeSlot.Empty)
                {
                    // repair
                    if (IsBroken && activeSlot.Itemstack.Collectible is ItemTemporalGear)
                    {
                        Block newBlock = world.GetBlock(CodeWithVariant("state", "normal"));
                        world.BlockAccessor.ExchangeBlock(newBlock.BlockId, blockSel.Position);
                        be.MarkDirty(true);

                        if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                        {
                            activeSlot.TakeOut(1);
                            activeSlot.MarkDirty();
                        }

                        if (api.Side == EnumAppSide.Server)
                        {
                            be.ActivateTeleportByPlayer(byPlayer.PlayerUID);
                        }

                        world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position.X + 0.5, blockSel.Position.Y, blockSel.Position.Z + 0.5, byPlayer, true, 16);
                        return true;
                    }

                    // change frame
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

            if (api.Side == EnumAppSide.Client)
            {
                var core = api.ModLoader.GetModSystem<Core>();
                core.HudCircleRenderer!.CircleVisible = false;
            }

            if (api.Side == EnumAppSide.Server)
            {
                var teleportManager = api.ModLoader.GetModSystem<TeleportManager>();
                teleportManager.Points.Remove(pos);
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
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
                        be.ActivateTeleportByPlayer(byPlayer.PlayerUID);
                    }
                }
            }

            return flag;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            var drops = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier) ?? Array.Empty<ItemStack>();
            if (world.BlockAccessor.GetBlockEntity(pos) is BETeleport be)
            {
                if (be.FrameStack.Collectible.Code != BETeleport.DefaultFrameCode)
                {
                    drops.AddItem(be.FrameStack);
                }
            }
            return drops;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer)
                .Append(WorldInteractions.ToArray());
        }
    }
}
