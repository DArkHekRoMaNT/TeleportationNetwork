using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class BlockTeleport : Block
    {
        public MeshData[] meshes = new MeshData[2];

        public Shape GetShape(EnumTeleportState state)
        {
            string path = "shapes/block/teleport/broken.json";
            if (state == EnumTeleportState.Ready) path = "shapes/block/teleport/ready.json";

            return api.Assets.Get(new AssetLocation(Constants.MOD_ID, path)).ToObject<Shape>();
        }

        public MeshData GetMesh(EnumTeleportState state)
        {
            if (meshes[(int)state] == null)
            {
                Shape shape = GetShape(state);
                ICoreClientAPI capi = api as ICoreClientAPI;
                MeshData mesh;
                capi.Tesselator.TesselateShape(this, shape, out mesh);

                meshes[(int)state] = mesh;
            }

            return meshes[(int)state];
        }

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            BlockEntityTeleport be = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityTeleport;
            if (be == null) return;

            be.OnEntityCollide(entity);
        }

        // TODO
        public override void OnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos)
        {
            base.OnEntityInside(world, entity, pos);
        }
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityTeleport be = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityTeleport;
            if (be == null) return false;

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            if (be.State == EnumTeleportState.Broken && slot.Itemstack.Collectible is ItemTemporalGear)
            {
                be.State = EnumTeleportState.Ready;

                if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    slot.TakeOut(1);
                    slot.MarkDirty();
                }

                world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position.X + 0.5, blockSel.Position.Y, blockSel.Position.Z + 0.5, byPlayer, true, 16);

                return true;
            }

            return true;
        }
    }
}