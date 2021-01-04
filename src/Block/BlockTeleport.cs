using System;
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
        public MeshData[] meshes = new MeshData[2];

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            BETeleport be = api.World.BlockAccessor.GetBlockEntity(pos) as BETeleport;
            if (be == null) return;

            be.OnEntityCollide(entity);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
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

                if (api.Side == EnumAppSide.Server) TPNetManager.AddAvailableTeleport(byPlayer as IServerPlayer, be.Pos);

                world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position.X + 0.5, blockSel.Position.Y, blockSel.Position.Z + 0.5, byPlayer, true, 16);

                return true;
            }

            return false;
        }
    }
}