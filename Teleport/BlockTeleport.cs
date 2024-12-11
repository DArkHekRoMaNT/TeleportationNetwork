using CommonLib.Utils;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace TeleportationNetwork
{
    public class BlockTeleport : Block
    {
        public BlockFacing Orientation { get; private set; } = null!;
        public float RotationDeg { get; private set; }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            Orientation = BlockFacing.FromCode(LastCodePart()) ?? BlockFacing.NORTH;
            RotationDeg = Orientation.Index switch
            {
                BlockFacing.indexNORTH => 0,
                BlockFacing.indexWEST => 90,
                BlockFacing.indexSOUTH => 180,
                BlockFacing.indexEAST => 270,
                _ => 0
            };
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
                if (be.Status.IsRepaired)
                {
                    be.OpenTeleportDialog();
                    return true;
                }

                // Repair
                var activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (!activeSlot.Empty)
                {
                    if (be.Status.IsBroken && activeSlot.Itemstack.Collectible.Code == GetRepairItem())
                    {
                        be.Status.Repair();
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

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            var size = byItemStack.Attributes.GetAsInt("size", 5);

            var flag = base.DoPlaceBlock(world, byPlayer, blockSel.AddPosCopy(0, size / 5, 0), byItemStack);

            if (world.BlockAccessor.GetBlockEntity(blockSel.Position.AddCopy(0, size / 5, 0)) is BlockEntityTeleport be)
            {
                be.Size = size;

                if (!byItemStack.Attributes.GetAsBool("broken", true))
                {
                    be.Status.Repair();
                }

                if (api.Side == EnumAppSide.Server)
                {
                    if (flag && byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
                    {
                        be.ActivateTeleportByPlayer(byPlayer.PlayerUID);
                    }
                }

                be.UpdateBlock();
            }

            return flag;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityTeleport be)
            {
                var itemStack = new ItemStack(this);
                itemStack.Attributes.SetBool("broken", be.Status.IsBroken);
                itemStack.Attributes.SetInt("size", be.Size);
            }

            return base.OnPickBlock(world, pos);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            return [OnPickBlock(world, pos)];
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            var interactions = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);

            if (world.BlockAccessor.GetBlockEntity(selection.Position) is BlockEntityTeleport be)
            {
                var repairItem = api.World.GetCollectibleObject(GetRepairItem());
                if (be.Status.IsBroken)
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

                if (be.Status.IsRepaired)
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

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (api is ICoreClientAPI capi)
            {
                var isCreative = capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative;
                var isLinker = capi.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible is ItemTeleportLeverLinker;
                if (!isLinker && !isCreative)
                {
                    return [];
                }
            }

            return CollisionBoxes;
        }
    }
}
