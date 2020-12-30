using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class BlockTeleport : Block, ITexPositionSource
    {
        #region  render

        ITexPositionSource tmpTextureSource;
        public Size2i AtlasSize { get { return tmpTextureSource.AtlasSize; } }
        public TextureAtlasPosition this[string textureCode] { get { return tmpTextureSource[textureCode]; } }

        public Shape GetShape(ICoreClientAPI capi, string type, string shapename, ITesselatorAPI tesselator = null, int altTexNumber = 0)
        {
            if (shapename == null) return null;
            if (tesselator == null) tesselator = capi.Tesselator;

            tmpTextureSource = tesselator.GetTexSource(this, altTexNumber);

            AssetLocation shapeloc = AssetLocation.Create(shapename, Code.Domain).WithPathPrefix("shapes/");
            Shape shape = capi.Assets.TryGet(shapeloc + ".json")?.ToObject<Shape>();

            return shape;
        }

        public MeshData GenMesh(ICoreClientAPI capi, string type, string shapename, ITesselatorAPI tesselator = null, Vec3f rotation = null, int altTexNumber = 0)
        {
            Shape shape = GetShape(capi, type, shapename, tesselator, altTexNumber);
            if (tesselator == null) tesselator = capi.Tesselator;
            if (shape == null) return new MeshData();

            MeshData mesh;
            tesselator.TesselateShape("octogram", shape, out mesh, this);
            return mesh;
        }

        #endregion

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityTeleport be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityTeleport;

            if (be != null)
            {
                be.OnRightClick(byPlayer);
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            BlockEntityTeleport be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityTeleport;

            if (be != null)
            {
                be.OnCollide(world, entity, pos, facing, collideSpeed, isImpact);
            }
        }

    }
}