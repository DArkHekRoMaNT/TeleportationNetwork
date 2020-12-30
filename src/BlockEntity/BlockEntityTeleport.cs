using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class BlockEntityTeleport : BlockEntity
    {
        long? collidems;
        long? lastcollidems;

        OctogramRender renderer;
        GuiDialogTeleport dialog;

        MeshData OctogramMesh
        {
            get
            {
                object value;
                Api.ObjectCache.TryGetValue("octogrammesh", out value);
                return (MeshData)value;
            }
            set { Api.ObjectCache["octogrammesh"] = value; }
        }

        internal MeshData GenMesh(string type = "octogram")
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            if (block.BlockId == 0) return null;

            MeshData mesh;
            ITesselatorAPI mesher = (Api as ICoreClientAPI).Tesselator;

            mesher.TesselateShape(block, Api.Assets.TryGet(new AssetLocation(
                Constants.MOD_ID, "shapes/block/teleport/" + type + ".json")).ToObject<Shape>(), out mesh
            );

            return mesh;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            RegisterGameTickListener(OnGameTick, 200);
            if (Api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                renderer = new OctogramRender(capi, Pos, GenMesh("octogram"));
                capi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "teleport");

                if (OctogramMesh == null)
                {
                    OctogramMesh = GenMesh("octogram");
                }
            }
        }

        private void OnGameTick(float dt)
        {
            if (lastcollidems != null && Api.World.ElapsedMilliseconds - lastcollidems > 1000)
            {
                collidems = null;
                lastcollidems = null;
            }
        }
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (Block == null) return false;

            mesher.AddMeshData(
                OctogramMesh.Clone()
                .Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, renderer.AngleRad, 0)
                .Translate(0 / 16f, 11 / 16f, 0 / 16f)
            );

            return true;
        }
        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            renderer?.Dispose();
        }
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            renderer?.Dispose();
            renderer = null;
        }
        public void OnRightClick(IPlayer byPlayer)
        {
            if (byPlayer?.Entity != null)
            {
                Api.SendMessageAll("Clicked");
            }
        }

        public void OnCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (collidems == null)
            {
                collidems = Api.World.ElapsedMilliseconds;
                Api.SendMessageAll(collidems + "");
            }
            lastcollidems = Api.World.ElapsedMilliseconds;

            if (Api.World.ElapsedMilliseconds - collidems > Constants.COOLDOWN)
            {
                Api.SendMessageAll("Open GUI");
                collidems = null;
            }
        }
    }
}