using CommonLib.Utils;
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
    public class BlockTeleport : Block, IMultiBlockColSelBoxes, IMultiBlockInteract
    {
        public bool IsBroken => Variant["state"] == "broken";
        public bool IsNormal => Variant["state"] == "normal";

        public TeleportParticleController? ParticleController { get; private set; }
        public GateSettings Gate { get; private set; } = null!;

        private readonly HashSet<BlockPos> _activated = [];

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            Gate = Attributes["gate"].AsObject<GateSettings>();

            if (api is ICoreClientAPI capi)
            {
                ParticleController = new TeleportParticleController(capi);
            }

            CollisionBoxes = [new Cuboidf(0, 0, Gate.Thick, 1, 1, Gate.Thick * 2).RotatedCopy(0, Gate.Rotation, 0, new Vec3d(0.5f, 0.5f, 0.5f))];
        }

        public override bool AllowSnowCoverage(IWorldAccessor world, BlockPos blockPos)
        {
            return true; //TODO: Snow
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

                // Select target
                if (IsNormal)
                {
                    be.OpenTeleportDialog();
                    return true;
                }

                // Repair
                var activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (!activeSlot.Empty)
                {
                    if (IsBroken && activeSlot.Itemstack.Collectible.Code == GetRepairItem())
                    {
                        Block newBlock = world.GetBlock(CodeWithVariant("state", "normal"));
                        world.BlockAccessor.ExchangeBlock(newBlock.BlockId, blockSel.Position);
                        be.UpdateBlock();

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

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityTeleport be)
            {
                be.OnEntityCollide(entity);
            }

            base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);
        }

        public override void OnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityTeleport be)
            {
                be.OnEntityCollide(entity);
            }

            base.OnEntityInside(world, entity, pos);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (Core.Config.Unbreakable && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                return;
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
            return base.CanPlaceBlock(world, byPlayer, blockSel.AddPosCopy(0, (int)Gate.Size / 5, 0), ref failureCode);
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool flag = base.DoPlaceBlock(world, byPlayer, blockSel.AddPosCopy(0, (int)Gate.Size / 5, 0), byItemStack);

            if (world.BlockAccessor.GetBlockEntity(blockSel.Position.AddCopy(0, (int)Gate.Size / 5, 0)) is BlockEntityTeleport be)
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
                interactions = interactions
                    .Append(new WorldInteraction
                    {
                        ActionLangCode = $"{Constants.ModId}:blockhelp-teleport-open",
                        MouseButton = EnumMouseButton.Right
                    })
                    .Append(new WorldInteraction
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

        public WorldInteraction[] MBGetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer, Vec3i offset)
        {
            return [];
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return IsActive(pos) ? CollisionBoxes : [];
        }

        public Cuboidf[] MBGetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            return IsActive(pos + offset.ToBlockPos()) ? CollisionBoxes : [];
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            CollisionBoxes = [new Cuboidf(0, 0, 0, 1, 1, Gate.Thick).RotatedCopy(0, Gate.Rotation, 0, new Vec3d(0.5f, 0.5f, 0.5f))];
            return CollisionBoxes;
        }

        public Cuboidf[] MBGetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            return MBGetCollisionBoxes(blockAccessor, pos, offset);
        }

        public bool IsActive(BlockPos pos)
        {
            return _activated.Contains(pos);
        }

        public void SetActive(bool active, BlockPos pos)
        {
            if (active)
            {
                _activated.Add(pos);
            }
            else
            {
                _activated.Remove(pos);
            }
        }

        #region Unused

        public bool MBDoParticalSelection(IWorldAccessor world, BlockPos pos, Vec3i offset)
        {
            return DoParticalSelection(world, pos + offset.ToBlockPos());
        }

        public bool MBOnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
            blockSel = blockSel.Clone();
            blockSel.Position += offset.ToBlockPos();
            return OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public bool MBOnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
            blockSel = blockSel.Clone();
            blockSel.Position += offset.ToBlockPos();
            return OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
        }

        public void MBOnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
            blockSel = blockSel.Clone();
            blockSel.Position += offset.ToBlockPos();
            OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }

        public bool MBOnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason, Vec3i offset)
        {
            blockSel = blockSel.Clone();
            blockSel.Position += offset.ToBlockPos();
            return OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
        }

        public ItemStack MBOnPickBlock(IWorldAccessor world, BlockPos pos, Vec3i offset)
        {
            return OnPickBlock(world, pos + offset.ToBlockPos());
        }

        public BlockSounds GetSounds(IBlockAccessor blockAccessor, BlockSelection blockSel, ItemStack stack, Vec3i offset)
        {
            blockSel = blockSel.Clone();
            blockSel.Position += offset.ToBlockPos();
            return GetSounds(blockAccessor, blockSel, stack);
        }

        #endregion
    }
}
