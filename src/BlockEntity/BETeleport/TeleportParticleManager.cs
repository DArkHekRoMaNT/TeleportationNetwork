using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class TeleportParticleManager
    {
        readonly ICoreClientAPI api;
        readonly BlockPos teleportPos;

        readonly SimpleParticleProperties particles;
        readonly Item temporalGear;

        Vec3d SealCenter => teleportPos.ToVec3d().Add(0.5, 1, 0.5);

        public TeleportParticleManager(ICoreClientAPI api, BlockPos teleportPos)
        {
            this.api = api;
            this.teleportPos = teleportPos;

            particles = new SimpleParticleProperties()
            {
                MinQuantity = 1,//0.3f,
                AddQuantity = 0,//1.0f,
                MinPos = new Vec3d(),
                AddPos = new Vec3d(),
                MinVelocity = new Vec3f(),
                AddVelocity = new Vec3f(),
                LifeLength = 0.5f,
                GravityEffect = -0.05f,
                MinSize = 0.1f,
                MaxSize = 0.2f,
                ParticleModel = EnumParticleModel.Quad,

                SelfPropelled = true,
                WindAffected = true,
                WindAffectednes = 0.05f,

                SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEARNULLIFY, -0.1f)
            };

            temporalGear = api.World.GetItem(new AssetLocation("gear-temporal"));
        }

        public void SpawnActiveParticles()
        {
            SpawnSealEdgeParticle();

            Entity[] entities = MathUtil.GetInCircleEntities(api, Constants.SealRadius, SealCenter);
            foreach (Entity entity in entities)
            {
                float radius = entity.CollisionBox.Width / 2f;
                Vec3d pos = entity.SidedPos.XYZ;

                int quantity = (int)(entity.CollisionBox.Width * entity.CollisionBox.Width * 5);
                SpawnEdgeParticle(radius, pos, quantity);
            }
        }

        private void SpawnParticle()
        {
            particles.Color = temporalGear.GetRandomColor(api, new ItemStack(temporalGear));
            api.World.SpawnParticles(particles);
        }

        private void SpawnEdgeParticle(float radius, Vec3d pos)
        {
            particles.MinPos.Set(MathUtil.RandomPosOnCircleEdge(radius, pos));
            SpawnParticle();
        }

        private void SpawnEdgeParticle(float radius, Vec3d pos, int quantity)
        {
            for (int i = 0; i < quantity; i++)
            {
                SpawnEdgeParticle(radius, pos);
            }
        }

        public void SpawnSealEdgeParticle(int quantity)
        {
            for (int i = 0; i < quantity; i++)
            {
                SpawnSealEdgeParticle();
            }
        }

        public void SpawnSealEdgeParticle() => SpawnEdgeParticle(Constants.SealRadius, SealCenter);
    }
}