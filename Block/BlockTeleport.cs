using CommonLib.Utils;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace TeleportationNetwork
{
    public class BlockTeleport : Block
    {
        private WorldInteraction FrameInteraction { get; set; } = new();

        public bool IsBroken => Variant["state"] == "broken";
        public bool IsNormal => Variant["state"] == "normal";

        public TeleportParticleController? ParticleController { get; private set; }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            InitWorldInteractions();
            if (api is ICoreClientAPI capi)
            {
                ParticleController = new TeleportParticleController(capi);
            }
        }

        private void InitWorldInteractions()
        {
            var frames = api.World.Blocks
                .Where((b) => b.DrawType == EnumDrawType.Cube)
                .Select((Block b) => new ItemStack(b))
                .ToArray();

            FrameInteraction = new WorldInteraction
            {
                ActionLangCode = $"{Constants.ModId}:blockhelp-teleport-change-frame",
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "sprint",
                Itemstacks = frames
            };
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

        public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
        {
            if (api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityTeleport be &&
                be.FrameStack?.Block != null)
            {
                return be.FrameStack.Block.GetRetention(pos, facing, type);
            }

            return base.GetRetention(pos, facing, type);
        }

        public override float GetResistance(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return Core.Config.Unbreakable ? 100000 : base.GetResistance(blockAccessor, pos);
        }

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityTeleport be)
            {
                be.OnEntityCollide(entity);
            }
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

                ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (!activeSlot.Empty)
                {
                    // repair
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

                    // change frame
                    if (byPlayer.Entity.Controls.Sprint &&
                        activeSlot.Itemstack.Class == EnumItemClass.Block &&
                        activeSlot.Itemstack.Block.DrawType == EnumDrawType.Cube &&
                        !activeSlot.Itemstack.Collectible.Equals(activeSlot.Itemstack, be.FrameStack))
                    {
                        if (byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative ||
                            world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
                        {
                            api.World.SpawnItemEntity(be.FrameStack, blockSel.Position.ToVec3d().Add(TopMiddlePos));
                            be.FrameStack = activeSlot.TakeOut(1);
                            be.MarkDirty(true);
                            return true;
                        }
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

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool flag = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityTeleport be)
            {
                if (api.Side == EnumAppSide.Server)
                {
                    if (flag && byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
                    {
                        be.ActivateTeleportByPlayer(byPlayer.PlayerUID);
                    }
                }

                string? frameCode = byItemStack?.Attributes.GetString("frameCode");
                if (frameCode != null)
                {
                    Block? frameBlock = world.GetBlock(new AssetLocation(frameCode));
                    if (frameBlock != null)
                    {
                        be.FrameStack = new ItemStack(frameBlock);
                    }
                }
            }

            return flag;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = base.OnPickBlock(world, pos) ?? new(this);
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityTeleport be)
            {
                AssetLocation? frameCode = be.FrameStack?.Collectible?.Code;
                if (frameCode != null)
                {
                    stack.Attributes.SetString("frameCode", frameCode.ToString());
                }
            }
            return stack;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack,
            EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            string? frameStackCode = itemstack.Attributes.GetString("frameCode");
            if (frameStackCode != null)
            {
                string key = $"{Constants.ModId}_teleportFrameMesh_{Code}_{frameStackCode}";
                renderinfo.ModelRef = ObjectCacheUtil.GetOrCreate(capi, key, () =>
                {
                    capi.Tesselator.TesselateBlock(this, out MeshData baseMesh);

                    Block? frameBlock = capi.World.GetBlock(new AssetLocation(frameStackCode));
                    if (frameBlock != null)
                    {
                        var shapeCode = new AssetLocation(Constants.ModId, "shapes/block/teleport/frame.json");
                        Shape frameShape = capi.Assets.Get<Shape>(shapeCode);
                        capi.Tesselator.TesselateShape(frameBlock, frameShape, out MeshData frameMesh);
                        baseMesh.AddMeshData(frameMesh);
                    }

                    return capi.Render.UploadMultiTextureMesh(baseMesh);
                });
            }
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
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

            return interactions.Append(FrameInteraction);
        }

        public AssetLocation GetRepairItem()
        {
            string item = Core.Config.TeleportRepairItem;
            if (item == null || api.World.GetCollectibleObject(new AssetLocation(item)) == null)
                return new AssetLocation("unknown");
            return new AssetLocation(item);
        }
    }
}
