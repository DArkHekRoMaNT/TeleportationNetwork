using CommonLib.Utils;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class BlockTeleport : Block, IMultiBlockColSelBoxes
    {
        public bool IsBroken => Variant["state"] == "broken";
        public bool IsNormal => Variant["state"] == "normal";

        public TeleportParticleController? ParticleController { get; private set; }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (api is ICoreClientAPI capi)
            {
                ParticleController = new TeleportParticleController(capi);
            }
        }

        public override bool AllowSnowCoverage(IWorldAccessor world, BlockPos blockPos)
        {
            return true;
        }

        public override double GetBlastResistance(IWorldAccessor world, BlockPos pos, Vec3f blastDirectionVector, EnumBlastType blastType)
        {
            return double.MaxValue;
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return [];
        }

        public override float GetResistance(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return Core.Config.Unbreakable ? 100000 : base.GetResistance(blockAccessor, pos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityTeleport be)
            {
                if (byPlayer.Entity.Controls.Sneak)
                {
                    be.OpenEditDialog();
                    return true;
                }

                // select target
                if (IsNormal)
                {
                    be.OpenTeleportDialog();
                    return true;
                }

                // repair
                ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (!activeSlot.Empty)
                {
                    if (IsBroken && activeSlot.Itemstack.Collectible.Code == GetRepairItem())
                    {
                        Block newBlock = world.GetBlock(CodeWithVariant("state", "normal"));
                        world.BlockAccessor.ExchangeBlock(newBlock.BlockId, blockSel.Position);
                        be.Update();

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
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (Core.Config.Unbreakable && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                return;
            }

            if (api.Side == EnumAppSide.Client)
            {
                var core = api.ModLoader.GetModSystem<Core>();
                core.HudCircleRenderer.CircleVisible = false;
            }

            if (api.Side == EnumAppSide.Server)
            {
                var teleportManager = api.ModLoader.GetModSystem<TeleportManager>();
                teleportManager.Points.Remove(pos);
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            ;
            return base.CanPlaceBlock(world, byPlayer, blockSel.AddPosCopy(0, 1, 0), ref failureCode);
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool flag = base.DoPlaceBlock(world, byPlayer, blockSel.AddPosCopy(0, 1, 0), byItemStack);

            if (world.BlockAccessor.GetBlockEntity(blockSel.Position.AddCopy(0, 1, 0)) is BlockEntityTeleport be)
            {
                if (api.Side == EnumAppSide.Server)
                {
                    if (flag && byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
                    {
                        be.ActivateTeleportByPlayer(byPlayer.PlayerUID);
                    }
                }
            }

            return flag;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            return [OnPickBlock(world, pos)];
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            var interactions = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);

            var repairItem = api.World.GetCollectibleObject(GetRepairItem());
            if (IsBroken)
            {
                if (repairItem != null)
                {
                    interactions = interactions.Append(new WorldInteraction
                    {
                        ActionLangCode = "blockhelp-translocator-repair-2",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = [new(repairItem)]
                    });
                }
                else
                {
                    interactions = interactions.Append(new WorldInteraction
                    {
                        ActionLangCode = $"{Constants.ModId}:blockhelp-teleport-unknownitem",
                        MouseButton = EnumMouseButton.Right
                    });
                }
            }

            if (IsNormal)
            {
                interactions = interactions.Append(new WorldInteraction
                {
                    ActionLangCode = $"{Constants.ModId}:blockhelp-teleport-edit",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sneak"
                });
            }

            return interactions;
        }

        public AssetLocation GetRepairItem()
        {
            string item = Core.Config.TeleportRepairItem;
            if (item == null || api.World.GetCollectibleObject(new AssetLocation(item)) == null)
                return new AssetLocation("unknown");
            return new AssetLocation(item);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return [];
        }

        public Cuboidf[] MBGetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            return [];
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (api is ICoreClientAPI capi && !capi.World.Player.Entity.Controls.CtrlKey)
                return [];
            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public Cuboidf[] MBGetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            return GetSelectionBoxes(blockAccessor, pos + offset.ToBlockPos());
        }
    }
}
